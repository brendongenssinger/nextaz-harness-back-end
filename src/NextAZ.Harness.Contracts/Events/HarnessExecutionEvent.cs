namespace NextAZ.Harness.Contracts.Events;
public sealed record HarnessExecutionEvent(Guid MessageId, Guid TenantId, Guid ExecutionId,
    string CorrelationId, string AgentId, int AgentVersion, string Channel, string Operation,
    DateTimeOffset OccurredAt, string EventType, string? ErrorCode = null, int SchemaVersion = 1);
