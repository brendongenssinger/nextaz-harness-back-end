namespace NextAZ.Harness.Infrastructure.Persistence;
internal sealed class OutboxEventRecord
{
    public Guid MessageId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public int AgentVersion { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public int SchemaVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
