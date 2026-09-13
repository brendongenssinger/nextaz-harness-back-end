using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NextAZ.Harness.Contracts.Events;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Infrastructure.Persistence;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Tests;
public sealed class PostgresPersistenceTests
{
    [PostgresFact] public async Task PersistsImmutableFingerprintForConflictingReplay()
    {
        var (factory, repository) = await CreateAsync();
        var tenant = Guid.NewGuid();
        var pending = Pending(tenant, "same-correlation", new string('a', 64));
        try
        {
            Assert.True(await repository.TryCreateAsync(pending, default));
            Assert.False(await repository.TryCreateAsync(Pending(tenant, "same-correlation",
                new string('b', 64)), default));
            var stored = await repository.GetByCorrelationAsync(tenant, pending.Context.Operation,
                pending.Context.CorrelationId, default);
            Assert.Equal(new string('a', 64), stored?.RequestFingerprint);
            Assert.Equal(pending.Context.ExecutionId, stored?.Context.ExecutionId);
        }
        finally { await Cleanup(factory, tenant); }
    }

    [PostgresFact] public async Task ExpiredLeaseIsReclaimedAndFencesStaleWriter()
    {
        var (factory, repository) = await CreateAsync();
        var tenant = Guid.NewGuid();
        var pending = Pending(tenant, Guid.NewGuid().ToString(), new string('c', 64));
        var firstToken = Guid.NewGuid();
        var secondToken = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        try
        {
            Assert.True(await repository.TryCreateAsync(pending, default));
            var first = await repository.TryAcquireLeaseAsync(tenant, pending.Context.ExecutionId.Value,
                "worker-1", firstToken, now, now.AddMinutes(1), default);
            Assert.Equal(firstToken, first?.LeaseToken);
            Assert.Null(await repository.TryAcquireLeaseAsync(tenant, pending.Context.ExecutionId.Value,
                "worker-2", secondToken, now.AddSeconds(30), now.AddMinutes(2), default));
            var reclaimed = await repository.TryAcquireLeaseAsync(tenant, pending.Context.ExecutionId.Value,
                "worker-2", secondToken, now.AddMinutes(2), now.AddMinutes(3), default);
            Assert.Equal(secondToken, reclaimed?.LeaseToken);
            var staleTerminal = Terminal(first!, "stale");
            Assert.False(await repository.TryFinalizeAsync(staleTerminal, firstToken,
                Audit(staleTerminal, Guid.NewGuid()), default));
            var current = await repository.GetAsync(tenant, pending.Context.ExecutionId.Value, default);
            Assert.Equal(AgentExecutionStatus.Running, current?.Status);
            Assert.Equal(secondToken, current?.LeaseToken);
            var winner = Terminal(reclaimed!, "winner");
            Assert.True(await repository.TryFinalizeAsync(winner, secondToken,
                Audit(winner, Guid.NewGuid()), default));
            Assert.False(await repository.TryFinalizeAsync(winner with { ResponseContent = "late overwrite" },
                secondToken, Audit(winner, Guid.NewGuid()), default));
            var completed = await repository.GetAsync(tenant, pending.Context.ExecutionId.Value, default);
            Assert.Equal(AgentExecutionStatus.Completed, completed?.Status);
            Assert.Equal("winner", completed?.ResponseContent);
            Assert.Null(completed?.LeaseToken);
            await using var db = await factory.CreateDbContextAsync();
            Assert.Equal(1, await db.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM harness.outbox_events WHERE tenant_id = {0}",
                tenant).SingleAsync());
        }
        finally { await Cleanup(factory, tenant); }
    }

    [PostgresFact] public async Task TerminalStateAndAuditRollbackTogetherWhenAuditInsertFails()
    {
        var (factory, repository) = await CreateAsync();
        var tenant = Guid.NewGuid();
        var pending = Pending(tenant, Guid.NewGuid().ToString(), new string('d', 64));
        var token = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var duplicateMessageId = Guid.NewGuid();
        var existingEventType = "preexisting.audit";
        try
        {
            Assert.True(await repository.TryCreateAsync(pending, default));
            var running = await repository.TryAcquireLeaseAsync(tenant, pending.Context.ExecutionId.Value,
                "worker", token, now, now.AddMinutes(1), default);
            Assert.NotNull(running);
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO harness.outbox_events
                    (message_id, tenant_id, execution_id, correlation_id, agent_id, agent_version, channel,
                     operation, occurred_at, event_type, schema_version, created_at)
                    VALUES ({duplicateMessageId}, {tenant}, {pending.Context.ExecutionId.Value},
                     {pending.Context.CorrelationId}, {pending.Context.AgentId.Value},
                     {pending.Context.AgentVersion.Value}, {pending.Context.Channel}, {pending.Context.Operation},
                     {now}, {existingEventType}, {1}, {now})");
            }
            var terminal = Terminal(running!, "must-not-commit");
            await Assert.ThrowsAsync<DbUpdateException>(() => repository.TryFinalizeAsync(terminal, token,
                Audit(terminal, duplicateMessageId), default));
            var stored = await repository.GetAsync(tenant, pending.Context.ExecutionId.Value, default);
            Assert.Equal(AgentExecutionStatus.Running, stored?.Status);
            Assert.Equal(token, stored?.LeaseToken);
            Assert.Null(stored?.ResponseContent);
            await using var verification = await factory.CreateDbContextAsync();
            Assert.Equal(0, await verification.Database.SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM harness.outbox_events WHERE tenant_id = {0} AND event_type = 'harness.execution.completed.v1'",
                tenant).SingleAsync());
        }
        finally { await Cleanup(factory, tenant); }
    }

    private static async Task<(PooledDbContextFactory<HarnessDbContext> Factory,
        PostgresExecutionRepository Repository)> CreateAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("HARNESS_POSTGRES_TEST")!;
        var options = new DbContextOptionsBuilder<HarnessDbContext>().UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "harness")).Options;
        var factory = new PooledDbContextFactory<HarnessDbContext>(options);
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        return (factory, new PostgresExecutionRepository(factory, TimeProvider.System));
    }
    private static ExecutionSnapshot Pending(Guid tenant, string correlation, string fingerprint)
    {
        var context = new HarnessContext(tenant, new AgentId("imobiliaria.atendimento"), new AgentVersion(1),
            ExecutionId.New(), correlation, "api", "imobiliaria.atendimento", DateTimeOffset.UtcNow);
        return new(context, AgentExecutionStatus.Pending, DateTimeOffset.UtcNow,
            null, null, null, 0, 0, null, null, [], RequestFingerprint: fingerprint);
    }
    private static ExecutionSnapshot Terminal(ExecutionSnapshot running, string response) => running with
    {
        Status = AgentExecutionStatus.Completed,
        EndedAt = DateTimeOffset.UtcNow,
        ProviderId = "openai-responses",
        ModelId = "configured-model",
        InputTokens = 3,
        OutputTokens = 2,
        ResponseContent = response
    };
    private static HarnessExecutionEvent Audit(ExecutionSnapshot terminal, Guid messageId) => new(messageId,
        terminal.Context.TenantId, terminal.Context.ExecutionId.Value, terminal.Context.CorrelationId,
        terminal.Context.AgentId.Value, terminal.Context.AgentVersion.Value, terminal.Context.Channel,
        terminal.Context.Operation, terminal.EndedAt!.Value, "harness.execution.completed.v1");
    private static async Task Cleanup(PooledDbContextFactory<HarnessDbContext> factory, Guid tenant)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM harness.outbox_events WHERE tenant_id = {tenant}");
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM harness.executions WHERE tenant_id = {tenant}");
    }
}

[AttributeUsage(AttributeTargets.Method)]
internal sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HARNESS_POSTGRES_TEST")))
            Skip = "Set HARNESS_POSTGRES_TEST to run the required PostgreSQL integration tests.";
    }
}
