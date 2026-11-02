namespace NextAZ.Harness.Contracts.Administration;

public sealed record HarnessUsageReport(
    Guid TenantId,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TotalExecutions,
    int CompletedExecutions,
    int FailedExecutions,
    long InputTokens,
    long OutputTokens,
    decimal KnownEstimatedCost,
    int ExecutionsWithKnownCost,
    int ExecutionsWithoutKnownCost,
    string CostCurrency,
    IReadOnlyList<HarnessUsageGroup> Groups);

public sealed record HarnessUsageGroup(
    string AgentId,
    int AgentVersion,
    string Operation,
    string Provider,
    string Model,
    int TotalExecutions,
    int CompletedExecutions,
    int FailedExecutions,
    long InputTokens,
    long OutputTokens,
    decimal KnownEstimatedCost,
    int ExecutionsWithKnownCost,
    int ExecutionsWithoutKnownCost);
