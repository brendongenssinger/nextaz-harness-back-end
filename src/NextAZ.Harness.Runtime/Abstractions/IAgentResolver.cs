namespace NextAZ.Harness.Runtime.Abstractions;
public interface IAgentResolver
{
    Task<AgentRouteTarget?> ResolveAsync(Guid tenantId, string channel, string operation, CancellationToken cancellationToken);
}

public sealed record AgentRouteTarget(AgentId AgentId, AgentVersion? AgentVersion);
