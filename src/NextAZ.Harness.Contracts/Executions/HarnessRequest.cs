namespace NextAZ.Harness.Contracts.Executions;
public sealed record HarnessRequest(Guid TenantId, string Channel, string Operation,
    string Content, string CorrelationId, IReadOnlyDictionary<string, string>? Metadata = null,
    IReadOnlyList<HarnessAttachment>? Attachments = null);
