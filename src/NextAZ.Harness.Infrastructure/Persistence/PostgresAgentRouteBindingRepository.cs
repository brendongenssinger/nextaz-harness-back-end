using Microsoft.EntityFrameworkCore;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class PostgresAgentRouteBindingRepository(
    IDbContextFactory<HarnessDbContext> factory,
    TimeProvider timeProvider) : IAgentRouteBindingRepository
{
    public async Task<AgentRouteBinding?> GetAsync(Guid tenantId, string channel, string operation,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.AgentRouteBindings.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Channel == channel && x.Operation == operation,
            cancellationToken);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<AgentRouteBinding>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var records = await db.AgentRouteBindings.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Channel)
            .ThenBy(x => x.Operation)
            .ToArrayAsync(cancellationToken);
        return records.Select(ToDomain).ToArray();
    }

    public async Task<AgentRouteBinding> SaveAsync(AgentRouteBindingUpdate update,
        CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await db.AgentRouteBindings.SingleOrDefaultAsync(
            x => x.TenantId == update.TenantId && x.Channel == update.Channel &&
                x.Operation == update.Operation,
            cancellationToken);

        if ((record is null && update.ExpectedRevision is not null) ||
            (record is not null && update.ExpectedRevision != record.Revision))
            throw new AgentRouteRevisionConflictException();

        var now = timeProvider.GetUtcNow();
        if (record is null)
        {
            record = new AgentRouteBindingRecord
            {
                TenantId = update.TenantId,
                Channel = update.Channel,
                Operation = update.Operation,
                Revision = 1
            };
            db.AgentRouteBindings.Add(record);
        }
        else
        {
            record.Revision++;
        }

        record.AgentId = update.AgentId.Value;
        record.AgentVersion = update.AgentVersion.Value;
        record.Enabled = update.Enabled;
        record.UpdatedAt = now;
        record.UpdatedBy = update.UpdatedBy;
        db.AgentRouteBindingEvents.Add(new AgentRouteBindingAuditRecord
        {
            EventId = Guid.NewGuid(),
            TenantId = record.TenantId,
            Channel = record.Channel,
            Operation = record.Operation,
            AgentId = record.AgentId,
            AgentVersion = record.AgentVersion,
            Enabled = record.Enabled,
            Revision = record.Revision,
            OccurredAt = now,
            Actor = update.UpdatedBy
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception is DbUpdateConcurrencyException || IsUniqueViolation(exception))
        {
            throw new AgentRouteRevisionConflictException();
        }

        return ToDomain(record);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };

    private static AgentRouteBinding ToDomain(AgentRouteBindingRecord record) =>
        new(record.TenantId, record.Channel, record.Operation, new AgentId(record.AgentId),
            new AgentVersion(record.AgentVersion), record.Enabled, record.Revision,
            record.UpdatedAt, record.UpdatedBy);
}
