using NextAZ.Harness.Infrastructure.Configuration;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.AgentResolution;

namespace NextAZ.Harness.Runtime.Tests;

public sealed class PersistedAgentResolverTests
{
    [Fact]
    public async Task Persisted_binding_overrides_static_route_and_disabled_binding_blocks_it()
    {
        var tenant = Guid.NewGuid();
        var key = (tenant, "api", "funcionario-digital.recibo");
        var fallback = new AgentId("static.receipt");
        var repository = new InMemoryAgentRouteBindingRepository(TimeProvider.System);
        var resolver = new PersistedAgentResolver(repository,
            new ConfiguredAgentResolver(new Dictionary<(Guid, string, string), AgentId> { [key] = fallback }));

        Assert.Equal(fallback, (await resolver.ResolveAsync(tenant, key.Item2, key.Item3, default))?.AgentId);

        var dynamicAgent = new AgentId("funcionario-digital.recibo");
        var enabled = await repository.SaveAsync(new AgentRouteBindingUpdate(
            tenant, key.Item2, key.Item3, dynamicAgent, new AgentVersion(1), true, null, "test"), default);
        var selected = await resolver.ResolveAsync(tenant, key.Item2, key.Item3, default);
        Assert.Equal(dynamicAgent, selected?.AgentId);
        Assert.Equal(new AgentVersion(1), selected?.AgentVersion);

        await repository.SaveAsync(new AgentRouteBindingUpdate(
            tenant, key.Item2, key.Item3, dynamicAgent, new AgentVersion(1), false, enabled.Revision, "test"), default);
        Assert.Null(await resolver.ResolveAsync(tenant, key.Item2, key.Item3, default));
    }

    [Fact]
    public async Task Save_requires_matching_revision_after_creation()
    {
        var tenant = Guid.NewGuid();
        var repository = new InMemoryAgentRouteBindingRepository(TimeProvider.System);
        var update = new AgentRouteBindingUpdate(tenant, "api", "test.route", new AgentId("test.agent"),
            new AgentVersion(1), true, null, "test");
        await repository.SaveAsync(update, default);

        await Assert.ThrowsAsync<AgentRouteRevisionConflictException>(() => repository.SaveAsync(update, default));
    }
}
