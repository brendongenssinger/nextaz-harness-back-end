namespace NextAZ.Harness.Runtime.Abstractions;

public interface IAgentRouteBindingRepository
{
    Task<AgentRouteBinding?> GetAsync(Guid tenantId, string channel, string operation,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRouteBinding>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AgentRouteBinding> SaveAsync(AgentRouteBindingUpdate update, CancellationToken cancellationToken);
}
