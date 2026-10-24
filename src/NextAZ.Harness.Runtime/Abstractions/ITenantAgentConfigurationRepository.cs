namespace NextAZ.Harness.Runtime.Abstractions;
public interface ITenantAgentConfigurationRepository
{
    Task<TenantAgentConfiguration?> GetAsync(Guid tenantId, AgentId agentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TenantAgentConfiguration>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken);
}
