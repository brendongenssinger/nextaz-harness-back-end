namespace NextAZ.Harness.Runtime.AgentResolution;
public sealed class ConfiguredAgentResolver(IReadOnlyDictionary<(Guid TenantId, string Channel, string Operation), AgentId> routes) : IAgentResolver
{
    public Task<AgentId?> ResolveAsync(Guid tenantId, string channel, string operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant required.", nameof(tenantId));
        return Task.FromResult(routes.TryGetValue((tenantId, channel, operation), out var id) ? id : null);
    }
}
