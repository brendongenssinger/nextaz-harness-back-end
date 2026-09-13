namespace NextAZ.Harness.Runtime.Execution;
public sealed record AgentExecutionResult(string Content, string ProviderId, string ModelId,
    int InputTokens, int OutputTokens, decimal? EstimatedCost);
