using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.Tooling;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.Runtime.ModelRouting;
public sealed record ModelExecutionRequest(HarnessContext Context, ModelRoute Route,
    string Objective, string TenantInstructions, string Content, MemoryContext Memory,
    IReadOnlyCollection<ToolDefinition> Tools, IReadOnlyList<ToolResult> ToolResults, int MaxOutputTokens,
    IReadOnlyList<NextAZ.Harness.Domain.Conversations.ConversationMessage>? Messages = null,
    IReadOnlyDictionary<string, string>? ResourceReferences = null,
    IReadOnlyList<HarnessAttachment>? Attachments = null);
