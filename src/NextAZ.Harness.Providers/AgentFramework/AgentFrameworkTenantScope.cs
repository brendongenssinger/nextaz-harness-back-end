namespace NextAZ.Harness.Providers.AgentFramework;

public sealed class AgentFrameworkTenantScope(IEnumerable<Guid> tenantIds, IEnumerable<string>? agentIds = null)
{
    private readonly IReadOnlySet<Guid> _tenantIds = tenantIds.ToHashSet();
    private readonly IReadOnlySet<string> _agentIds = (agentIds ?? ["cartorio.atendimento"])
        .ToHashSet(StringComparer.Ordinal);

    public bool Allows(Guid tenantId) => tenantId != Guid.Empty && _tenantIds.Contains(tenantId);
    public bool Allows(Guid tenantId, string agentId) => Allows(tenantId) && _agentIds.Contains(agentId);
}
