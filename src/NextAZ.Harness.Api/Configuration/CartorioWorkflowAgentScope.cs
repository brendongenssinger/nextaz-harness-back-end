using System.Collections.Frozen;

namespace NextAZ.Harness.Api.Configuration;

public sealed class CartorioWorkflowAgentScope(bool enabled, IEnumerable<Guid> tenantIds,
    IEnumerable<string> channels, IEnumerable<string> agentIds)
{
    private readonly IReadOnlySet<Guid> _tenantIds = tenantIds.ToFrozenSet();
    public bool Enabled { get; } = enabled;
    public IReadOnlyList<string> Channels { get; } = channels.Distinct(StringComparer.Ordinal).ToArray();
    public IReadOnlySet<string> AgentIds { get; } = agentIds.ToFrozenSet(StringComparer.Ordinal);
    public bool Allows(Guid tenantId) => Enabled && tenantId != Guid.Empty && _tenantIds.Contains(tenantId);
    public bool Allows(Guid tenantId, string agentId) => Allows(tenantId) && AgentIds.Contains(agentId);
}
