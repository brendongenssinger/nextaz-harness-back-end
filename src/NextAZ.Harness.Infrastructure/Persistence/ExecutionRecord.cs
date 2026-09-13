namespace NextAZ.Harness.Infrastructure.Persistence;
internal sealed class ExecutionRecord
{
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public int AgentVersion { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset ContextTimestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string? ProviderId { get; set; }
    public string? ModelId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? ErrorCode { get; set; }
    public string StepsJson { get; set; } = "[]";
    public string? ResponseContent { get; set; }
    public string RequestFingerprint { get; set; } = string.Empty;
    public string? LeaseOwner { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
}
