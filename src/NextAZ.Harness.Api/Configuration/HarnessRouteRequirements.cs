using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Policies;

namespace NextAZ.Harness.Api.Configuration;

public sealed record HarnessRouteRequirement(Guid TenantId, AgentId AgentId,
    AgentVersion AgentVersion, ModelPolicy ModelPolicy);

public sealed class HarnessRouteRequirements(IEnumerable<HarnessRouteRequirement> items)
{
    public IReadOnlyList<HarnessRouteRequirement> Items { get; } = items.ToArray();
}
