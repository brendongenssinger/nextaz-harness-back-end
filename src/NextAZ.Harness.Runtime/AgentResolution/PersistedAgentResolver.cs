using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Runtime.AgentResolution;

public sealed class PersistedAgentResolver(
    IAgentRouteBindingRepository bindings,
    ConfiguredAgentResolver configured) : IAgentResolver
{
    public async Task<AgentRouteTarget?> ResolveAsync(Guid tenantId, string channel, string operation,
        CancellationToken cancellationToken)
    {
        var binding = await bindings.GetAsync(tenantId, channel, operation, cancellationToken);
        if (binding is not null)
            return binding.Enabled ? new AgentRouteTarget(binding.AgentId, binding.AgentVersion) : null;

        return await configured.ResolveAsync(tenantId, channel, operation, cancellationToken);
    }
}
