using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Conversations;
namespace NextAZ.Harness.Application.Executions;
// Internal, resolved by Application. Never bind this type directly from an HTTP body.
public sealed record ResolvedExecutionProfile(AgentDefinition Agent,
    TenantAgentConfiguration Configuration, IReadOnlyList<ConversationMessage> Messages,
    bool DisableTools, bool SimulationOnly);
