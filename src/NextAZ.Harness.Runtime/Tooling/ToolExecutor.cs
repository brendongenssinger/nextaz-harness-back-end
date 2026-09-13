using NextAZ.Harness.Runtime.Execution;
namespace NextAZ.Harness.Runtime.Tooling;
public sealed class ToolExecutor(IToolRegistry registry, IExecutionPolicyEvaluator policies) : IToolExecutor
{
    public async Task<ToolResult> ExecuteAsync(AgentExecutionRequest execution, ToolCall call, CancellationToken cancellationToken)
    {
        policies.Validate(execution.Context, execution.Agent, execution.Configuration, execution.Permissions);
        var handler = registry.Find(call.ToolId);
        if (handler is null || !execution.Agent.AllowedTools.Contains(call.ToolId) ||
            !execution.Configuration.AllowedTools.Contains(call.ToolId) ||
            !execution.Tools.Any(t => t.ToolId == call.ToolId) ||
            !execution.Permissions.Contains(handler.Definition.RequiredPermission))
            throw new HarnessExecutionException("tool_forbidden");
        // Writes are allowed only through an adapter whose backend owns durable idempotency and approvals.
        if (handler.Definition.RiskLevel != ToolRiskLevel.Read && handler is not IBackendAuthorizedWriteToolHandler)
            throw new HarnessExecutionException("tool_write_not_enabled");
        if (string.IsNullOrWhiteSpace(call.CallId) || call.CallId.Length > 128 ||
            call.Arguments.ValueKind != System.Text.Json.JsonValueKind.Object ||
            call.Arguments.GetRawText().Length > 16384 || !handler.ValidateArguments(call.Arguments))
            throw new HarnessExecutionException("tool_arguments_invalid");
        if (call.ToolId.Value.StartsWith("imobiliaria.", StringComparison.Ordinal) &&
            execution.ResourceReferences?.TryGetValue("atendimentoId", out var atendimentoId) == true &&
            (!call.Arguments.TryGetProperty("atendimentoId", out var supplied) ||
             supplied.ValueKind != System.Text.Json.JsonValueKind.String ||
             !string.Equals(supplied.GetString(), atendimentoId, StringComparison.OrdinalIgnoreCase)))
            throw new HarnessExecutionException("tool_resource_forbidden");
        if (handler.Definition.Timeout <= TimeSpan.Zero || handler.Definition.Timeout > TimeSpan.FromMinutes(2))
            throw new HarnessExecutionException("tool_timeout_invalid");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(handler.Definition.Timeout);
        execution.RecordStep("tool.started:" + call.ToolId.Value);
        try
        {
            var output = await handler.ExecuteAsync(execution.Context, call.Arguments, timeout.Token).WaitAsync(timeout.Token);
            if (output.GetRawText().Length > 32768) throw new HarnessExecutionException("tool_output_too_large");
            execution.RecordStep("tool.completed:" + call.ToolId.Value);
            return new ToolResult(call.CallId, call.ToolId, output, call.Arguments.Clone());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            execution.RecordStep("tool.timed_out:" + call.ToolId.Value);
            throw new HarnessExecutionException("tool_timed_out");
        }
    }
}
