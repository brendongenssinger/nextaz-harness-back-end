using System.Collections.Frozen;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;
namespace NextAZ.Harness.Infrastructure.Configuration;
public sealed class InMemoryAgentRegistry(IEnumerable<AgentDefinition> agents) : IAgentRegistry
{
    private readonly IReadOnlyDictionary<AgentId, AgentDefinition> _agents = agents
        .Select(a => a with { AllowedTools = a.AllowedTools.ToFrozenSet(), RequiredPolicies = a.RequiredPolicies.ToFrozenSet() })
        .ToFrozenDictionary(a => a.AgentId);
    public Task<AgentDefinition?> GetAsync(AgentId agentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_agents.GetValueOrDefault(agentId));
    }
}
