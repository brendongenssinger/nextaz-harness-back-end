using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Domain.Tools;
namespace NextAZ.Harness.Domain.Agents;
public sealed record AgentDefinition(AgentId AgentId, AgentVersion Version, string Name,
    string Vertical, string Objective, bool Enabled, ModelPolicy ModelPolicy,
    MemoryPolicy MemoryPolicy, IReadOnlySet<ToolId> AllowedTools,
    IReadOnlySet<string> RequiredPolicies, bool RequiresTenantContext = true,
    StructuredOutputDefinition? StructuredOutput = null);
