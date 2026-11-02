using NextAZ.Harness.Runtime.Memory;
using NextAZ.Harness.Runtime.Tooling;
namespace NextAZ.Harness.Runtime.ModelRouting;
public sealed record ModelExecutionRequest(HarnessContext Context, ModelRoute Route,
    string Objective, string TenantInstructions, string Content, MemoryContext Memory,
    IReadOnlyCollection<ToolDefinition> Tools, IReadOnlyList<ToolResult> ToolResults, int MaxOutputTokens,
    IReadOnlyList<NextAZ.Harness.Domain.Conversations.ConversationMessage>? Messages = null,
    IReadOnlyDictionary<string, string>? ResourceReferences = null,
    NextAZ.Harness.Domain.Agents.StructuredOutputDefinition? StructuredOutput = null,
    IReadOnlyList<ModelMediaInput>? MediaInputs = null);
