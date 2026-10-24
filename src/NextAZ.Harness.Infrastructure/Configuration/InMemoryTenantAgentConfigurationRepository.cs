using System.Collections.Frozen;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;
namespace NextAZ.Harness.Infrastructure.Configuration;
public sealed class InMemoryTenantAgentConfigurationRepository(IEnumerable<TenantAgentConfiguration> configurations)
    : ITenantAgentConfigurationRepository
{
    private readonly IReadOnlyDictionary<(Guid, AgentId), TenantAgentConfiguration> _configurations = configurations
        .Select(c => c with { AllowedTools = c.AllowedTools.ToFrozenSet() })
        .ToFrozenDictionary(c => (c.TenantId, c.AgentId));
    public Task<TenantAgentConfiguration?> GetAsync(Guid tenantId, AgentId agentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return Task.FromResult(_configurations.GetValueOrDefault((tenantId, agentId)));
    }
    public Task<IReadOnlyList<TenantAgentConfiguration>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return Task.FromResult<IReadOnlyList<TenantAgentConfiguration>>(_configurations.Values
            .Where(configuration => configuration.TenantId == tenantId)
            .OrderBy(configuration => configuration.AgentId.Value, StringComparer.Ordinal).ToArray());
    }
}
