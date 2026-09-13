using NextAZ.Harness.Domain.Agents;
namespace NextAZ.Harness.Domain.Executions;
public sealed record ExecutionContext
{
    public Guid TenantId { get; }
    public AgentId AgentId { get; }
    public AgentVersion AgentVersion { get; }
    public ExecutionId ExecutionId { get; }
    public string CorrelationId { get; }
    public string Channel { get; }
    public string Operation { get; }
    public DateTimeOffset Timestamp { get; }
    public ExecutionContext(Guid tenantId, AgentId agentId, AgentVersion agentVersion,
        ExecutionId executionId, string correlationId, string channel, string operation, DateTimeOffset timestamp)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentNullException.ThrowIfNull(agentId);
        ArgumentNullException.ThrowIfNull(agentVersion);
        ArgumentNullException.ThrowIfNull(executionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);
        TenantId = tenantId; AgentId = agentId; AgentVersion = agentVersion; ExecutionId = executionId;
        CorrelationId = correlationId; Channel = channel; Operation = operation; Timestamp = timestamp;
    }
}
