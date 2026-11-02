namespace NextAZ.Harness.Contracts.Receipts;

public sealed record ClassifyReceiptRequest(
    Guid TenantId,
    Guid RequestId,
    string MediaUrl);
