namespace NextAZ.Harness.Contracts.Receipts;

public sealed record ReceiptClassification(
    bool IsReceipt,
    string Label,
    decimal Score,
    decimal Threshold,
    bool RequiresDetailedExtraction);
