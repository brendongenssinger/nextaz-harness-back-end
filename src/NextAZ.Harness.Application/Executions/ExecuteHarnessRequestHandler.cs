using System.Text.RegularExpressions;
using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Events;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.Execution;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Application.Executions;
public sealed class ExecuteHarnessRequestHandler(ICurrentTenant tenant, IAgentResolver resolver,
    IAgentRegistry agents, ITenantAgentConfigurationRepository configurations,
    IExecutionPolicyEvaluator policies, IMemoryProvider memory, IModelRouter router,
    IToolRegistry tools, IAgentRuntime runtime, IExecutionRepository executions,
    IExecutionTelemetry telemetry, TimeProvider time)
    : IRequestHandler<ExecuteHarnessRequest, HarnessResponse>
{
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    public async Task<HarnessResponse> Handle(ExecuteHarnessRequest command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var proposedId = ExecutionId.New();
        HarnessResponse Reject(string code) => new(proposedId.Value, request.CorrelationId, null, null,
            "Failed", null, new(code, "The execution was rejected."));
        if (tenant.TenantId == Guid.Empty || tenant.TenantId != request.TenantId)
            return Reject("tenant_forbidden");
        if (!tenant.Permissions.Contains("harness.execute")) return Reject("execution_forbidden");
        if (!IsValid(request)) return Reject("invalid_request");

        var previous = await executions.GetByCorrelationAsync(tenant.TenantId, request.Operation,
            request.CorrelationId, cancellationToken);
        var agentId = command.Profile?.Agent.AgentId ?? previous?.Context.AgentId ??
            await resolver.ResolveAsync(tenant.TenantId, request.Channel, request.Operation, cancellationToken);
        if (agentId is null) return Reject("agent_not_found");
        var agent = command.Profile?.Agent ?? await agents.GetAsync(agentId, cancellationToken);
        if (agent is null) return Reject("agent_not_found");
        var configuration = command.Profile?.Configuration ??
            await configurations.GetAsync(tenant.TenantId, agentId, cancellationToken);
        if (configuration is null) return Reject("tenant_agent_not_configured");
        var fingerprint = ExecutionRequestFingerprint.Compute(request, agent, configuration,
            command.Profile?.Messages, command.ResourceReferences);

        var now = time.GetUtcNow();
        var context = previous?.Context ?? new HarnessContext(tenant.TenantId, agentId, agent.Version, proposedId,
            request.CorrelationId, request.Channel, request.Operation, now);
        var steps = previous?.Steps.ToList() ?? [];
        void Step(string name) => steps.Add(new(name, time.GetUtcNow()));
        if (previous is null)
        {
            Step("request.received"); Step("tenant.resolved"); Step("agent.resolved");
            var pending = new ExecutionSnapshot(context, AgentExecutionStatus.Pending, now,
                null, null, null, 0, 0, null, null, steps.ToArray(), RequestFingerprint: fingerprint);
            if (!await executions.TryCreateAsync(pending, cancellationToken))
                previous = await executions.GetByCorrelationAsync(tenant.TenantId, request.Operation,
                    request.CorrelationId, cancellationToken);
            else previous = pending;
        }
        if (previous is null) return Reject("execution_conflict");
        if (!string.Equals(previous.RequestFingerprint, fingerprint, StringComparison.Ordinal))
            return Reject("execution_conflict");
        if (IsTerminal(previous.Status)) return ToResponse(previous);

        var leaseToken = Guid.NewGuid();
        now = time.GetUtcNow();
        var leaseExpiresAt = now.Add(configuration.Limits.Timeout).AddSeconds(10);
        var snapshot = await executions.TryAcquireLeaseAsync(tenant.TenantId,
            previous.Context.ExecutionId.Value, _leaseOwner, leaseToken, now, leaseExpiresAt, cancellationToken);
        if (snapshot is null)
        {
            var active = await executions.GetAsync(tenant.TenantId, previous.Context.ExecutionId.Value,
                cancellationToken);
            return active is null ? Reject("execution_conflict") : ToResponse(active);
        }
        context = snapshot.Context;
        steps = snapshot.Steps.ToList();
        Step("execution.lease.acquired");
        string? content = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(configuration.Limits.Timeout);
        try
        {
            policies.Validate(context, agent, configuration, tenant.Permissions);
            Step("policies.evaluated");
            var memoryContext = await memory.LoadAsync(context, configuration, timeout.Token);
            if (memoryContext.TenantId != tenant.TenantId ||
                memoryContext.KnowledgeBaseId != configuration.KnowledgeBaseId)
                throw new HarnessExecutionException("memory_scope_mismatch");
            Step("context.loaded");
            var routes = await router.ResolveAsync(tenant.TenantId, configuration.ModelPolicy, timeout.Token);
            Step("model.route.resolved");
            if (command.Profile?.SimulationOnly == true &&
                (routes.Count == 0 || routes.Any(r => r.ProviderId != "simulation")))
                throw new HarnessExecutionException("employee_draft_provider_not_enabled");
            var allowedTools = command.Profile?.DisableTools == true
                ? Array.Empty<NextAZ.Harness.Domain.Tools.ToolDefinition>()
                : await tools.GetAllowedToolsAsync(agent, configuration, tenant.Permissions, timeout.Token);
            Step("tools.authorized"); Step("runtime.started");
            var result = await runtime.ExecuteAsync(new(context, agent, configuration,
                tenant.Permissions, request.Content, memoryContext, routes, allowedTools, Step,
                command.Profile?.Messages, command.ResourceReferences), timeout.Token).WaitAsync(timeout.Token);
            content = result.Content;
            snapshot = snapshot with { Status = AgentExecutionStatus.Completed, ProviderId = result.ProviderId,
                ModelId = result.ModelId, InputTokens = result.InputTokens, OutputTokens = result.OutputTokens,
                EstimatedCost = result.EstimatedCost, ResponseContent = content };
            Step("response.produced");
        }
        catch (OperationCanceledException)
        {
            snapshot = snapshot with { Status = cancellationToken.IsCancellationRequested
                ? AgentExecutionStatus.Cancelled : AgentExecutionStatus.TimedOut,
                ErrorCode = cancellationToken.IsCancellationRequested ? "execution_cancelled" : "execution_timed_out" };
        }
        catch (HarnessExecutionException exception)
        {
            snapshot = snapshot with { Status = exception.Code == "execution_timed_out"
                ? AgentExecutionStatus.TimedOut : AgentExecutionStatus.Failed, ErrorCode = exception.Code };
        }
        catch (Exception)
        {
            // Do not expose raw provider exceptions, prompts, credentials or customer content.
            snapshot = snapshot with { Status = AgentExecutionStatus.Failed, ErrorCode = "execution_failed" };
        }
        Step("execution.finalized");
        snapshot = snapshot with { EndedAt = time.GetUtcNow(), Steps = steps.ToArray() };
        if (snapshot.ErrorCode is not null) content = null;
        var audit = new HarnessExecutionEvent(Guid.NewGuid(), context.TenantId,
            context.ExecutionId.Value, context.CorrelationId, context.AgentId.Value,
            context.AgentVersion.Value, context.Channel, context.Operation, snapshot.EndedAt.Value,
            TerminalEventType(snapshot.Status), snapshot.ErrorCode);
        using var finalize = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        if (!await executions.TryFinalizeAsync(snapshot, leaseToken, audit, finalize.Token))
        {
            var current = await executions.GetAsync(context.TenantId, context.ExecutionId.Value, finalize.Token);
            return current is null ? Reject("execution_conflict") : ToResponse(current);
        }
        telemetry.Record(snapshot with { LeaseOwner = null, LeaseToken = null, LeaseExpiresAt = null });
        return ToResponse(snapshot);
    }
    private static bool IsTerminal(AgentExecutionStatus status) => status is
        AgentExecutionStatus.Completed or AgentExecutionStatus.Failed or AgentExecutionStatus.Cancelled or
        AgentExecutionStatus.TimedOut;
    private static string TerminalEventType(AgentExecutionStatus status) => status switch
    {
        AgentExecutionStatus.Completed => "harness.execution.completed.v1",
        AgentExecutionStatus.Cancelled => "harness.execution.cancelled.v1",
        AgentExecutionStatus.TimedOut => "harness.execution.timed-out.v1",
        _ => "harness.execution.failed.v1"
    };
    private static HarnessResponse ToResponse(ExecutionSnapshot snapshot) =>
        new(snapshot.Context.ExecutionId.Value, snapshot.Context.CorrelationId,
            snapshot.Context.AgentId.Value, snapshot.Context.AgentVersion.Value,
            snapshot.Status.ToString(), snapshot.ResponseContent,
            snapshot.ErrorCode is null ? null : new(snapshot.ErrorCode, "The execution did not complete."),
            snapshot.ProviderId, snapshot.ModelId, snapshot.InputTokens, snapshot.OutputTokens,
            snapshot.EstimatedCost);
    private static bool IsValid(HarnessRequest r) =>
        !string.IsNullOrWhiteSpace(r.Content) && r.Content.Length <= 16000 &&
        ValidIdentifier(r.Channel, 64) && ValidIdentifier(r.Operation, 120) && ValidIdentifier(r.CorrelationId, 128) &&
        (r.Metadata is null || (r.Metadata.Count <= 20 &&
            r.Metadata.All(p => ValidIdentifier(p.Key, 64) && p.Value is not null && p.Value.Length <= 1024)));
    private static bool ValidIdentifier(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max && Regex.IsMatch(value, @"^[a-zA-Z0-9._:-]+$");
}
