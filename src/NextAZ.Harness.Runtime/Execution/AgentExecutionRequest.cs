using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.ModelRouting;
namespace NextAZ.Harness.Runtime.Execution;
public sealed record AgentExecutionRequest(HarnessContext Context, AgentDefinition Agent,
    TenantAgentConfiguration Configuration, IReadOnlySet<string> Permissions, string Content,
    MemoryContext Memory, IReadOnlyList<ModelRoute> Routes, IReadOnlyCollection<ToolDefinition> Tools,
    Action<string> RecordStep, IReadOnlyList<NextAZ.Harness.Domain.Conversations.ConversationMessage>? Messages = null,
    IReadOnlyDictionary<string, string>? ResourceReferences = null);
