namespace NextAZ.Harness.Runtime.Tooling;
public sealed class ToolRegistry(IEnumerable<IToolHandler> handlers) : IToolRegistry
{
    private readonly IReadOnlyDictionary<ToolId, IToolHandler> _handlers = handlers.ToDictionary(h => h.Definition.ToolId);
    public IToolHandler? Find(ToolId id) => _handlers.GetValueOrDefault(id);
    public Task<IReadOnlyCollection<ToolDefinition>> GetAllowedToolsAsync(AgentDefinition agent,
        TenantAgentConfiguration configuration, IReadOnlySet<string> permissions, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (configuration.TenantId == Guid.Empty || configuration.AgentId != agent.AgentId)
            throw new Execution.HarnessExecutionException("tenant_context_mismatch");
        IReadOnlyCollection<ToolDefinition> result = _handlers.Values.Select(h => h.Definition)
            .Where(t => agent.AllowedTools.Contains(t.ToolId) && configuration.AllowedTools.Contains(t.ToolId)
                && permissions.Contains(t.RequiredPermission) && t.RiskLevel == ToolRiskLevel.Read).ToArray();
        return Task.FromResult(result);
    }
}
