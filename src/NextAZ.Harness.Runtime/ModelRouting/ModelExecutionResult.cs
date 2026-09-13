using NextAZ.Harness.Runtime.Tooling;
namespace NextAZ.Harness.Runtime.ModelRouting;
public sealed record ModelExecutionResult(string? Content, IReadOnlyList<ToolCall> ToolCalls,
    int InputTokens, int OutputTokens, decimal? EstimatedCost);
