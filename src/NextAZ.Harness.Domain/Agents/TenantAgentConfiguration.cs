using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Domain.Tools;
namespace NextAZ.Harness.Domain.Agents;
public sealed record TenantAgentConfiguration(Guid TenantId, AgentId AgentId, bool Enabled,
    int ConfigurationVersion, string Instructions, IReadOnlySet<ToolId> AllowedTools,
    Guid? KnowledgeBaseId, ModelPolicy ModelPolicy, MemoryPolicy MemoryPolicy, ExecutionLimits Limits);
