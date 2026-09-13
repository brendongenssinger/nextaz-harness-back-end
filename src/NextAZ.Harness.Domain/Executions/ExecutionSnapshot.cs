namespace NextAZ.Harness.Domain.Executions;
public sealed record ExecutionSnapshot(ExecutionContext Context, AgentExecutionStatus Status,
    DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? ProviderId, string? ModelId,
    int InputTokens, int OutputTokens, decimal? EstimatedCost,
    string? ErrorCode, IReadOnlyList<ExecutionStep> Steps, string? ResponseContent = null,
    string? RequestFingerprint = null, string? LeaseOwner = null, Guid? LeaseToken = null,
    DateTimeOffset? LeaseExpiresAt = null);
