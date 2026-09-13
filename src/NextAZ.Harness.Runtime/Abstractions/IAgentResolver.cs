namespace NextAZ.Harness.Runtime.Abstractions;
public interface IAgentResolver
{
    Task<AgentId?> ResolveAsync(Guid tenantId, string channel, string operation, CancellationToken cancellationToken);
}
