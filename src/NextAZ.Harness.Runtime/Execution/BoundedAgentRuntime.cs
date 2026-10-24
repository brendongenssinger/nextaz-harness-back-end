using System.Text.Json;
using NextAZ.Harness.Runtime.ModelRouting;
using NextAZ.Harness.Runtime.Tooling;
namespace NextAZ.Harness.Runtime.Execution;
public sealed class BoundedAgentRuntime(IEnumerable<IModelProvider> providers, IToolExecutor toolExecutor,
    IExecutionPolicyEvaluator policies) : IAgentRuntime
{
    private readonly IReadOnlyDictionary<string, IModelProvider> _providers = providers.ToDictionary(p => p.ProviderId);
    public async Task<AgentExecutionResult> ExecuteAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
    {
        policies.Validate(request.Context, request.Agent, request.Configuration, request.Permissions);
        if (request.Memory.TenantId != request.Context.TenantId ||
            request.Memory.KnowledgeBaseId != request.Configuration.KnowledgeBaseId)
            throw new HarnessExecutionException("memory_scope_mismatch");
        if (request.Routes.Count is < 1 or > 4) throw new HarnessExecutionException("model_route_unavailable");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(request.Configuration.Limits.Timeout);
        try { return await RunAsync(request, deadline.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new HarnessExecutionException("execution_timed_out"); }
    }
    private async Task<AgentExecutionResult> RunAsync(AgentExecutionRequest request, CancellationToken cancellationToken)
    {
        var results = new List<ToolResult>();
        var seenCalls = new HashSet<string>(StringComparer.Ordinal);
        var seenArguments = new HashSet<string>(StringComparer.Ordinal);
        int inputTokens = 0, outputTokens = 0;
        decimal? cost = 0;
        for (var step = 0; step < request.Configuration.Limits.MaxSteps; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (route, result) = await InvokeModelAsync(request, results, cancellationToken);
            if (result.InputTokens < 0 || result.OutputTokens < 0 ||
                result.OutputTokens > request.Configuration.Limits.MaxOutputTokens || result.EstimatedCost < 0 ||
                result.ToolCalls is null || result.ToolCalls.Count > request.Configuration.Limits.MaxToolCalls ||
                result.Content?.Length > 65536)
                throw new HarnessExecutionException("model_output_invalid");
            inputTokens = checked(inputTokens + result.InputTokens);
            outputTokens = checked(outputTokens + result.OutputTokens);
            cost = cost.HasValue && result.EstimatedCost.HasValue ? cost + result.EstimatedCost : null;
            request.RecordStep("model.completed:" + route.ProviderId + "/" + route.ModelId);
            if (result.ToolCalls.Count == 0)
            {
                if (string.IsNullOrWhiteSpace(result.Content)) throw new HarnessExecutionException("model_output_invalid");
                return new(result.Content, route.ProviderId, route.ModelId, inputTokens, outputTokens, cost);
            }
            if (results.Count + result.ToolCalls.Count > request.Configuration.Limits.MaxToolCalls)
                throw new HarnessExecutionException("tool_limit_exceeded");
            foreach (var call in result.ToolCalls)
            {
                if (call.Arguments.ValueKind != JsonValueKind.Object ||
                    !seenCalls.Add(call.CallId) || !seenArguments.Add(call.ToolId.Value + ":" + call.Arguments.GetRawText()))
                    throw new HarnessExecutionException("tool_call_repeated_or_invalid");
                // Re-evaluate authorization at invocation, even if the provider ignores the advertised allowlist.
                results.Add(await toolExecutor.ExecuteAsync(request, call, cancellationToken));
            }
        }
        throw new HarnessExecutionException("step_limit_exceeded");
    }
    private async Task<(ModelRoute Route, ModelExecutionResult Result)> InvokeModelAsync(
        AgentExecutionRequest request, IReadOnlyList<ToolResult> results, CancellationToken cancellationToken)
    {
        foreach (var route in request.Routes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_providers.TryGetValue(route.ProviderId, out var provider))
                throw new HarnessExecutionException("provider_not_configured");
            if (string.IsNullOrWhiteSpace(route.ModelId) || route.Timeout <= TimeSpan.Zero ||
                route.Timeout > request.Configuration.Limits.Timeout)
                throw new HarnessExecutionException("model_route_invalid");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(route.Timeout);
            try
            {
                request.RecordStep("model.started:" + route.ProviderId + "/" + route.ModelId);
                var input = new ModelExecutionRequest(request.Context, route, request.Agent.Objective,
                    request.Configuration.Instructions, request.Content, request.Memory, request.Tools,
                    results, request.Configuration.Limits.MaxOutputTokens, request.Messages,
                    request.ResourceReferences, request.Attachments);
                return (route, await provider.ExecuteAsync(input, timeout.Token).WaitAsync(timeout.Token));
            }
            catch (ModelProviderUnavailableException)
            { request.RecordStep("model.unavailable:" + route.ProviderId); }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { request.RecordStep("model.timed_out:" + route.ProviderId); }
        }
        throw new HarnessExecutionException("provider_unavailable");
    }
}
