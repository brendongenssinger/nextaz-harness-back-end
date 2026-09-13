using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Events;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Infrastructure.Persistence;
// Development/test adapter only. State transitions mirror the durable fencing contract.
public sealed class InMemoryExecutionRepository(int capacity = 1000, TimeProvider? timeProvider = null) : IExecutionRepository
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly object _gate = new();
    private readonly Dictionary<(Guid TenantId, Guid ExecutionId), ExecutionSnapshot> _items = [];
    private readonly Dictionary<(Guid TenantId, string Operation, string CorrelationId), Guid> _correlations = [];
    private readonly List<HarnessExecutionEvent> _auditEvents = [];
    public Task<bool> TryCreateAsync(ExecutionSnapshot execution, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateNew(execution);
        lock (_gate)
        {
            var correlation = (execution.Context.TenantId, execution.Context.Operation, execution.Context.CorrelationId);
            if (_correlations.ContainsKey(correlation)) return Task.FromResult(false);
            if (_items.Count >= capacity)
                throw new InvalidOperationException("Development execution store capacity reached.");
            var id = execution.Context.ExecutionId.Value;
            _items[(execution.Context.TenantId, id)] = Clone(execution);
            _correlations[correlation] = id;
            return Task.FromResult(true);
        }
    }
    public Task<ExecutionSnapshot?> TryAcquireLeaseAsync(Guid tenantId, Guid executionId, string leaseOwner,
        Guid leaseToken, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateLease(tenantId, leaseOwner, leaseToken, now, expiresAt);
        lock (_gate)
        {
            var key = (tenantId, executionId);
            if (!_items.TryGetValue(key, out var current) ||
                current.Status != AgentExecutionStatus.Pending &&
                !(current.Status == AgentExecutionStatus.Running &&
                  (current.LeaseExpiresAt is null || current.LeaseExpiresAt <= now)))
                return Task.FromResult<ExecutionSnapshot?>(null);
            var leased = current with { Status = AgentExecutionStatus.Running, LeaseOwner = leaseOwner,
                LeaseToken = leaseToken, LeaseExpiresAt = expiresAt };
            _items[key] = Clone(leased);
            return Task.FromResult<ExecutionSnapshot?>(Clone(leased));
        }
    }
    public Task<bool> TryFinalizeAsync(ExecutionSnapshot execution, Guid leaseToken,
        HarnessExecutionEvent auditEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTerminal(execution, leaseToken, auditEvent);
        lock (_gate)
        {
            var key = (execution.Context.TenantId, execution.Context.ExecutionId.Value);
            if (!_items.TryGetValue(key, out var current) || current.Status != AgentExecutionStatus.Running ||
                current.LeaseToken != leaseToken || current.LeaseExpiresAt <= _timeProvider.GetUtcNow())
                return Task.FromResult(false);
            var terminal = execution with { LeaseOwner = null, LeaseToken = null, LeaseExpiresAt = null };
            _items[key] = Clone(terminal);
            _auditEvents.Add(auditEvent);
            return Task.FromResult(true);
        }
    }
    public Task<ExecutionSnapshot?> GetAsync(Guid tenantId, Guid executionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTenant(tenantId);
        lock (_gate) return Task.FromResult(_items.TryGetValue((tenantId, executionId), out var item)
            ? Clone(item) : null);
    }
    public Task<ExecutionSnapshot?> GetByCorrelationAsync(Guid tenantId, string operation,
        string correlationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateTenant(tenantId);
        lock (_gate)
        {
            return Task.FromResult(_correlations.TryGetValue((tenantId, operation, correlationId), out var id) &&
                _items.TryGetValue((tenantId, id), out var item) ? Clone(item) : null);
        }
    }
    public IReadOnlyList<HarnessExecutionEvent> GetAuditEventsForTenant(Guid tenantId)
    {
        ValidateTenant(tenantId);
        lock (_gate) return _auditEvents.Where(x => x.TenantId == tenantId).ToArray();
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
    private static ExecutionSnapshot Clone(ExecutionSnapshot execution) =>
        execution with { Steps = Array.AsReadOnly(execution.Steps.ToArray()) };
}
