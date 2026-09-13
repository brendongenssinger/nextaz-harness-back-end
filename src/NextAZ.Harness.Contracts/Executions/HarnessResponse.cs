namespace NextAZ.Harness.Contracts.Executions;
public sealed record HarnessResponse(Guid ExecutionId, string CorrelationId, string? AgentId,
    int? AgentVersion, string Status, string? Content, HarnessError? Error,
    string? ProviderId = null, string? ModelId = null, int InputTokens = 0,
    int OutputTokens = 0, decimal? EstimatedCost = null);
