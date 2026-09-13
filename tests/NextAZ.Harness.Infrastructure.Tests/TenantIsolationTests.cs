using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Infrastructure.Configuration;
using NextAZ.Harness.Infrastructure.Persistence;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Tests;
public sealed class TenantIsolationTests
{
    [Fact] public async Task ExecutionLookupIncludesTenantBeforeReturningData()
    {
        var repository = new InMemoryExecutionRepository();
        var tenant = Guid.NewGuid();
        var id = ExecutionId.New();
        var context = new HarnessContext(tenant, new("cartorio.atendimento"), new(1), id,
            "corr", "api", "atendimento", DateTimeOffset.UtcNow);
        await repository.TryCreateAsync(new(context, AgentExecutionStatus.Pending, DateTimeOffset.UtcNow,
            null, null, null, 0, 0, 0, null, [], RequestFingerprint: new string('a', 64)), default);
        Assert.NotNull(await repository.GetAsync(tenant, id.Value, default));
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), id.Value, default));
    }
    [Fact] public async Task ConfigurationIsScopedToTenant()
    {
        var tenant = Guid.NewGuid();
        var agent = new AgentId("cartorio.atendimento");
        var configuration = new TenantAgentConfiguration(tenant, agent, true, 1, "private instructions",
            new HashSet<ToolId>(), null, new("fast-cheap"), MemoryPolicy.None, new());
        var repository = new InMemoryTenantAgentConfigurationRepository([configuration]);
        Assert.NotNull(await repository.GetAsync(tenant, agent, default));
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), agent, default));
    }
    [Fact] public async Task RepositoryRejectsMissingTenant()
    {
        var repository = new InMemoryExecutionRepository();
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(Guid.Empty, Guid.NewGuid(), default));
    }
    [Fact] public async Task ConfiguredAllowlistCannotBeMutatedAfterRegistration()
    {
        var allowed = new HashSet<ToolId>();
        var tenant = Guid.NewGuid();
        var agent = new AgentId("cartorio.atendimento");
        var repository = new InMemoryTenantAgentConfigurationRepository([
            new(tenant, agent, true, 1, "", allowed, null, new("fast-cheap"), MemoryPolicy.None, new())]);
        allowed.Add(new("billing.verify-payment"));
        Assert.Empty((await repository.GetAsync(tenant, agent, default))!.AllowedTools);
    }
}
