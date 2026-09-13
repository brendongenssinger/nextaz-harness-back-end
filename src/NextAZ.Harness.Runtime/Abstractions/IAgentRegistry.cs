namespace NextAZ.Harness.Runtime.Abstractions;
public interface IAgentRegistry
{
    Task<AgentDefinition?> GetAsync(AgentId agentId, CancellationToken cancellationToken);
}
