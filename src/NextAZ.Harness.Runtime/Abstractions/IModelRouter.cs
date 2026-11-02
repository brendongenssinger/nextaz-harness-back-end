namespace NextAZ.Harness.Runtime.Abstractions;
public interface IModelRouter
{
    Task<IReadOnlyList<ModelRouting.ModelRoute>> ResolveAsync(Guid tenantId, AgentId agentId,
        AgentVersion agentVersion, ModelPolicy policy, CancellationToken cancellationToken);
}
