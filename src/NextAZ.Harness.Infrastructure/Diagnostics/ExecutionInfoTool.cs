using System.Text.Json;
using NextAZ.Harness.Domain.Tools;
using NextAZ.Harness.Runtime.Tooling;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Diagnostics;
public sealed class ExecutionInfoTool : IToolHandler
{
    public ToolDefinition Definition { get; } = new(new("harness.execution-info"), "Execution context",
        "Returns identifiers of the current execution. Development diagnostic only.",
        """{"type":"object","properties":{},"additionalProperties":false}""",
        ToolRiskLevel.Read, true, TimeSpan.FromSeconds(2), "harness.execute");
    public bool ValidateArguments(JsonElement arguments) =>
        arguments.ValueKind == JsonValueKind.Object && !arguments.EnumerateObject().Any();
    public Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(JsonSerializer.SerializeToElement(new {
            context.TenantId, ExecutionId = context.ExecutionId.Value, context.CorrelationId
        }));
    }
}
