namespace NextAZ.Harness.Runtime.Abstractions;
public interface ITenantAgentConfigurationRepository
{
    Task<TenantAgentConfiguration?> GetAsync(Guid tenantId, AgentId agentId, CancellationToken cancellationToken);
}
