using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Observability;
public sealed class LoggingExecutionTelemetry(ILogger<LoggingExecutionTelemetry> logger) : IExecutionTelemetry
{
    public const string SourceName = "NextAZ.Harness";
    private static readonly ActivitySource Source = new(SourceName);
    private static readonly Meter Meter = new(SourceName, "0.1.0");
    private static readonly Counter<long> Completed = Meter.CreateCounter<long>("harness.executions");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("harness.execution.duration", "ms");
    public void Record(ExecutionSnapshot execution)
    {
        var context = execution.Context;
        var duration = (execution.EndedAt - execution.StartedAt)?.TotalMilliseconds ?? 0;
        using var activity = Source.StartActivity("harness.execution.summary");
        activity?.SetTag("tenant.id", context.TenantId.ToString());
        activity?.SetTag("execution.id", context.ExecutionId.Value.ToString());
        activity?.SetTag("correlation.id", context.CorrelationId);
        activity?.SetTag("agent.id", context.AgentId.Value);
        activity?.SetTag("agent.version", context.AgentVersion.Value);
        activity?.SetTag("provider.id", execution.ProviderId);
        activity?.SetTag("model.id", execution.ModelId);
        activity?.SetStatus(execution.ErrorCode is null ? ActivityStatusCode.Ok : ActivityStatusCode.Error, execution.ErrorCode);
        Completed.Add(1, new KeyValuePair<string, object?>("status", execution.Status.ToString()));
        Duration.Record(duration);
        logger.LogInformation(
            "Harness TenantId={TenantId} ExecutionId={ExecutionId} CorrelationId={CorrelationId} AgentId={AgentId} AgentVersion={AgentVersion} Channel={Channel} Operation={Operation} Status={Status} Provider={Provider} Model={Model} DurationMs={DurationMs} InputTokens={InputTokens} OutputTokens={OutputTokens} EstimatedCost={EstimatedCost} ErrorCode={ErrorCode}",
            context.TenantId, context.ExecutionId.Value, context.CorrelationId, context.AgentId.Value,
            context.AgentVersion.Value, context.Channel, context.Operation, execution.Status,
            execution.ProviderId, execution.ModelId, duration, execution.InputTokens,
            execution.OutputTokens, execution.EstimatedCost, execution.ErrorCode);
    }
}
