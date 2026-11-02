using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Application.Cartorio;

public sealed class GetCartorioWorkflowAgentsHandler(ICurrentTenant tenant, IAgentRegistry agents,
    ITenantAgentConfigurationRepository configurations, IModelRouter router)
    : IRequestHandler<GetCartorioWorkflowAgents, CartorioWorkflowAgentCatalogResponse>
{
    public async Task<CartorioWorkflowAgentCatalogResponse> Handle(GetCartorioWorkflowAgents request,
        CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty || !tenant.Permissions.Contains("harness.read"))
            throw new UnauthorizedAccessException();

        var definitions = (await agents.ListAsync(cancellationToken))
            .ToDictionary(agent => agent.AgentId);
        var configured = await configurations.ListAsync(tenant.TenantId, cancellationToken);
        var candidates = configured
            .Where(configuration => configuration.Enabled &&
                request.AllowedAgentIds.Contains(configuration.AgentId) &&
                definitions.TryGetValue(configuration.AgentId, out var definition) &&
                definition.Enabled && string.Equals(definition.Vertical, "cartorio", StringComparison.Ordinal))
            .Select(configuration => (Configuration: configuration,
                Definition: definitions[configuration.AgentId]))
            .OrderBy(agent => agent.Definition.Name, StringComparer.Ordinal)
            .ThenBy(agent => agent.Definition.AgentId.Value, StringComparer.Ordinal)
            .ToArray();
        var items = new List<CartorioWorkflowAgentCatalogItem>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var routes = await router.ResolveAsync(tenant.TenantId, candidate.Definition.AgentId,
                candidate.Definition.Version, candidate.Configuration.ModelPolicy, cancellationToken);
            if (routes.Count == 0) continue;
            var agent = candidate.Definition;
            items.Add(new(agent.AgentId.Value, agent.Name, request.Channels,
                [new(agent.Version.Value, "active")]));
        }
        return new(items);
    }
}
