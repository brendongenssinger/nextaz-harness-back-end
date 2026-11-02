using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Runtime.Tests;

public sealed class ConfiguredModelRouterTests
{
    [Fact]
    public async Task RouteIsIsolatedByTenantAgentAndPublishedVersion()
    {
        var tenant = Guid.NewGuid();
        var connection = Guid.NewGuid();
        var router = new ConfiguredModelRouter(
            new Dictionary<(Guid, string, int, string), IReadOnlyList<ModelRoute>>
            {
                [(tenant, "test.agent-a", 1, "economical")] =
                    [new("openai-responses", "gpt-4o-mini", TimeSpan.FromSeconds(10), connection)]
            });

        var selected = await router.ResolveAsync(tenant, new("test.agent-a"), new(1),
            new("economical"), default);
        var otherAgent = await router.ResolveAsync(tenant, new("test.agent-b"), new(1),
            new("economical"), default);
        var otherVersion = await router.ResolveAsync(tenant, new("test.agent-a"), new(2),
            new("economical"), default);
        var otherTenant = await router.ResolveAsync(Guid.NewGuid(), new("test.agent-a"), new(1),
            new("economical"), default);

        Assert.Single(selected);
        Assert.Equal(connection, selected[0].ProviderConnectionId);
        Assert.Empty(otherAgent);
        Assert.Empty(otherVersion);
        Assert.Empty(otherTenant);
    }
}
