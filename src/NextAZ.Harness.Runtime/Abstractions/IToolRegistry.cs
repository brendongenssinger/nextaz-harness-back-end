namespace NextAZ.Harness.Runtime.Abstractions;
public interface IToolRegistry
{
    Task<IReadOnlyCollection<ToolDefinition>> GetAllowedToolsAsync(AgentDefinition agent, TenantAgentConfiguration configuration, IReadOnlySet<string> permissions, CancellationToken cancellationToken);
    Tooling.IToolHandler? Find(ToolId toolId);
}
