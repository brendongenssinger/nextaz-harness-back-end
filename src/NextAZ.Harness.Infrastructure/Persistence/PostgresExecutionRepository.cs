using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Events;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Persistence;
public sealed class PostgresExecutionRepository(IDbContextFactory<HarnessDbContext> contextFactory,
    TimeProvider timeProvider) : IExecutionRepository
{
    public async Task<bool> TryCreateAsync(ExecutionSnapshot execution, CancellationToken cancellationToken)
    {
        ValidateNew(execution);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        db.Executions.Add(ToRecord(execution));
        try { await db.SaveChangesAsync(cancellationToken); return true; }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_executions_tenant_operation_correlation" })
        { return false; }
    }
    public async Task<ExecutionSnapshot?> TryAcquireLeaseAsync(Guid tenantId, Guid executionId,
        string leaseOwner, Guid leaseToken, DateTimeOffset now, DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        ValidateLease(tenantId, leaseOwner, leaseToken, now, expiresAt);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.Executions.Where(x => x.TenantId == tenantId && x.ExecutionId == executionId &&
            (x.Status == nameof(AgentExecutionStatus.Pending) ||
             x.Status == nameof(AgentExecutionStatus.Running) &&
             (x.LeaseExpiresAt == null || x.LeaseExpiresAt <= now)))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, nameof(AgentExecutionStatus.Running))
                .SetProperty(x => x.LeaseOwner, leaseOwner)
                .SetProperty(x => x.LeaseToken, leaseToken)
                .SetProperty(x => x.LeaseExpiresAt, expiresAt), cancellationToken);
        if (updated != 1) return null;
        var record = await db.Executions.AsNoTracking().SingleAsync(x =>
            x.TenantId == tenantId && x.ExecutionId == executionId, cancellationToken);
        return ToSnapshot(record);
    }
    public async Task<bool> TryFinalizeAsync(ExecutionSnapshot execution, Guid leaseToken,
        HarnessExecutionEvent auditEvent, CancellationToken cancellationToken)
    {
        ValidateTerminal(execution, leaseToken, auditEvent);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var steps = JsonSerializer.Serialize(execution.Steps);
        var now = timeProvider.GetUtcNow();
        var updated = await db.Executions.Where(x =>
            x.TenantId == execution.Context.TenantId &&
            x.ExecutionId == execution.Context.ExecutionId.Value &&
            x.Status == nameof(AgentExecutionStatus.Running) && x.LeaseToken == leaseToken &&
            x.LeaseExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, execution.Status.ToString())
                .SetProperty(x => x.EndedAt, execution.EndedAt)
                .SetProperty(x => x.ProviderId, execution.ProviderId)
                .SetProperty(x => x.ModelId, execution.ModelId)
                .SetProperty(x => x.InputTokens, execution.InputTokens)
                .SetProperty(x => x.OutputTokens, execution.OutputTokens)
                .SetProperty(x => x.EstimatedCost, execution.EstimatedCost)
                .SetProperty(x => x.ErrorCode, execution.ErrorCode)
                .SetProperty(x => x.StepsJson, steps)
                .SetProperty(x => x.ResponseContent, execution.ResponseContent)
                .SetProperty(x => x.LeaseOwner, (string?)null)
                .SetProperty(x => x.LeaseToken, (Guid?)null)
                .SetProperty(x => x.LeaseExpiresAt, (DateTimeOffset?)null), cancellationToken);
        if (updated != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }
        db.OutboxEvents.Add(ToOutbox(auditEvent));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
    public async Task<ExecutionSnapshot?> GetAsync(Guid tenantId, Guid executionId,
        CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Executions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.ExecutionId == executionId, cancellationToken);
        return record is null ? null : ToSnapshot(record);
    }
    public async Task<ExecutionSnapshot?> GetByCorrelationAsync(Guid tenantId, string operation,
        string correlationId, CancellationToken cancellationToken)
    {
        ValidateTenant(tenantId);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Executions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.TenantId == tenantId && x.Operation == operation && x.CorrelationId == correlationId,
            cancellationToken);
        return record is null ? null : ToSnapshot(record);
    }
    private ExecutionRecord ToRecord(ExecutionSnapshot execution)
    {
        var record = new ExecutionRecord
        {
            TenantId = execution.Context.TenantId,
            ExecutionId = execution.Context.ExecutionId.Value,
            AgentId = execution.Context.AgentId.Value,
            AgentVersion = execution.Context.AgentVersion.Value,
            CorrelationId = execution.Context.CorrelationId,
            Channel = execution.Context.Channel,
            Operation = execution.Context.Operation,
            ContextTimestamp = execution.Context.Timestamp,
            RequestFingerprint = execution.RequestFingerprint!
        };
        Apply(execution, record);
        return record;
    }
    private static void Apply(ExecutionSnapshot execution, ExecutionRecord record)
    {
        record.Status = execution.Status.ToString();
        record.StartedAt = execution.StartedAt;
        record.EndedAt = execution.EndedAt;
        record.ProviderId = execution.ProviderId;
        record.ModelId = execution.ModelId;
        record.InputTokens = execution.InputTokens;
        record.OutputTokens = execution.OutputTokens;
        record.EstimatedCost = execution.EstimatedCost;
        record.ErrorCode = execution.ErrorCode;
        record.StepsJson = JsonSerializer.Serialize(execution.Steps);
        record.ResponseContent = execution.ResponseContent;
        record.LeaseOwner = execution.LeaseOwner;
        record.LeaseToken = execution.LeaseToken;
        record.LeaseExpiresAt = execution.LeaseExpiresAt;
    }
    private OutboxEventRecord ToOutbox(HarnessExecutionEvent message) => new()
    {
        MessageId = message.MessageId,
        TenantId = message.TenantId,
        ExecutionId = message.ExecutionId,
        CorrelationId = message.CorrelationId,
        AgentId = message.AgentId,
        AgentVersion = message.AgentVersion,
        Channel = message.Channel,
        Operation = message.Operation,
        OccurredAt = message.OccurredAt,
        EventType = message.EventType,
        ErrorCode = message.ErrorCode,
        SchemaVersion = message.SchemaVersion,
        CreatedAt = timeProvider.GetUtcNow()
    };
    private static ExecutionSnapshot ToSnapshot(ExecutionRecord record)
    {
        if (!Enum.TryParse<AgentExecutionStatus>(record.Status, out var status))
            throw new InvalidOperationException("Stored execution status is invalid.");
        var steps = JsonSerializer.Deserialize<ExecutionStep[]>(record.StepsJson) ?? [];
        var context = new HarnessContext(record.TenantId, new AgentId(record.AgentId),
            new AgentVersion(record.AgentVersion), new ExecutionId(record.ExecutionId), record.CorrelationId,
            record.Channel, record.Operation, record.ContextTimestamp);
        return new(context, status, record.StartedAt, record.EndedAt, record.ProviderId, record.ModelId,
            record.InputTokens, record.OutputTokens, record.EstimatedCost, record.ErrorCode, steps,
            record.ResponseContent, record.RequestFingerprint, record.LeaseOwner, record.LeaseToken,
            record.LeaseExpiresAt);
    }
    private static void ValidateNew(ExecutionSnapshot execution)
    {
        if (execution.Status != AgentExecutionStatus.Pending || execution.LeaseToken is not null ||
            execution.LeaseOwner is not null || execution.LeaseExpiresAt is not null ||
            execution.RequestFingerprint is null || execution.RequestFingerprint.Length != 64)
            throw new ArgumentException("A new execution must be pending, unleased and fingerprinted.", nameof(execution));
    }
    private static void ValidateLease(Guid tenantId, string owner, Guid token, DateTimeOffset now,
        DateTimeOffset expiresAt)
    {
        ValidateTenant(tenantId);
        if (string.IsNullOrWhiteSpace(owner) || owner.Length > 200 || token == Guid.Empty || expiresAt <= now)
            throw new ArgumentException("A valid execution lease is required.");
    }
    private static void ValidateTerminal(ExecutionSnapshot execution, Guid leaseToken,
        HarnessExecutionEvent auditEvent)
    {
        if (execution.Status is AgentExecutionStatus.Pending or AgentExecutionStatus.Running or
            AgentExecutionStatus.WaitingExternalEvent || execution.EndedAt is null || leaseToken == Guid.Empty ||
            auditEvent.TenantId != execution.Context.TenantId ||
            auditEvent.ExecutionId != execution.Context.ExecutionId.Value ||
            auditEvent.MessageId == Guid.Empty || execution.LeaseToken != leaseToken ||
            auditEvent.CorrelationId != execution.Context.CorrelationId ||
            auditEvent.AgentId != execution.Context.AgentId.Value ||
            auditEvent.AgentVersion != execution.Context.AgentVersion.Value ||
            auditEvent.Channel != execution.Context.Channel || auditEvent.Operation != execution.Context.Operation ||
            auditEvent.OccurredAt != execution.EndedAt || auditEvent.SchemaVersion != 1 ||
            auditEvent.EventType != TerminalEventType(execution.Status) ||
            auditEvent.ErrorCode != execution.ErrorCode)
            throw new ArgumentException("A matching terminal execution and audit event are required.");
    }
    private static string TerminalEventType(AgentExecutionStatus status) => status switch
    {
        AgentExecutionStatus.Completed => "harness.execution.completed.v1",
        AgentExecutionStatus.Cancelled => "harness.execution.cancelled.v1",
        AgentExecutionStatus.TimedOut => "harness.execution.timed-out.v1",
        AgentExecutionStatus.Failed => "harness.execution.failed.v1",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };
    private static void ValidateTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
    }
}
