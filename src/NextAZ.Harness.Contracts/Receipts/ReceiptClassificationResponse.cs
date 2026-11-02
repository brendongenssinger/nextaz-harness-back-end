using NextAZ.Harness.Contracts.Executions;

namespace NextAZ.Harness.Contracts.Receipts;

public sealed record ReceiptClassificationResponse(
    Guid ExecutionId,
    string Status,
    ReceiptClassification? Classification,
    HarnessError? Error,
    string? ProviderId = null,
    string? ModelId = null,
    decimal? EstimatedCost = null);
