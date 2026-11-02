using System.Text.RegularExpressions;
using System.Collections.Frozen;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Api.Configuration;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Cartorio;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/cartorio/workflows/agents")]
public sealed class CartorioWorkflowAgentsController(ISender sender, ICurrentTenant tenant,
    CartorioWorkflowAgentScope scope) : ControllerBase
{
    private const string WorkflowOrigin = "nextaz-cartorio-workflow";

    [HttpGet]
    [Authorize(Policy = "CartorioRead")]
    public async Task<ActionResult<CartorioWorkflowAgentCatalogResponse>> Catalog(
        CancellationToken cancellationToken)
    {
        if (!scope.Allows(tenant.TenantId)) return NotFound();
        return Ok(await sender.Send(new GetCartorioWorkflowAgents(scope.Channels,
            scope.AgentIds.Select(id => new AgentId(id)).ToFrozenSet()), cancellationToken));
    }

    [HttpPost("{agentId}/versions/{version:int}/executions")]
    [Authorize(Policy = "CartorioExecute")]
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<HarnessResponse>> Execute(string agentId, int version,
        CartorioWorkflowAgentExecutionRequest request, CancellationToken cancellationToken)
        => await ExecuteCore(agentId,version,request,false,cancellationToken);

    [HttpPost("{agentId}/versions/{version:int}/responses")]
    [Authorize(Policy = "CartorioExecute")]
    [RequestSizeLimit(65536)]
    public Task<ActionResult<HarnessResponse>> GenerateResponse(string agentId,int version,
        CartorioWorkflowAgentExecutionRequest request,CancellationToken cancellationToken)
        => ExecuteCore(agentId,version,request,true,cancellationToken);

    private async Task<ActionResult<HarnessResponse>> ExecuteCore(string agentId,int version,
        CartorioWorkflowAgentExecutionRequest request,bool responseOnly,CancellationToken cancellationToken)
    {
        if (!scope.Allows(tenant.TenantId)) return NotFound();
        if (!scope.Allows(tenant.TenantId, agentId)) return NotFound();
        if (!TryAgentId(agentId, out var parsedAgentId) || version < 1 ||
            !TryBuildOrigin(request, out var origin))
            return BadRequest(Failed(request.CorrelationId, "invalid_request"));

        var result = await sender.Send(new ExecuteCartorioWorkflowAgent(parsedAgentId!, version,
            scope.Channels.ToHashSet(StringComparer.Ordinal),
            scope.AgentIds.Select(id => new AgentId(id)).ToFrozenSet(), request, origin!, responseOnly),
            cancellationToken);
        if (result.Status == "Completed")
            return CreatedAtAction(nameof(Get), new { agentId, version, executionId = result.ExecutionId }, result);
        if (result.Status is "Pending" or "Running")
            return AcceptedAtAction(nameof(Get), new { agentId, version, executionId = result.ExecutionId }, result);
        return StatusCode(StatusFor(result), result);
    }

    [HttpGet("{agentId}/versions/{version:int}/executions/{executionId:guid}")]
    [Authorize(Policy = "CartorioRead")]
    public async Task<ActionResult<HarnessResponse>> Get(string agentId, int version, Guid executionId,
        CancellationToken cancellationToken)
    {
        if (!scope.Allows(tenant.TenantId)) return NotFound();
        if (!scope.Allows(tenant.TenantId, agentId) ||
            !TryAgentId(agentId, out var parsedAgentId) || version < 1) return NotFound();
        var execution = await sender.Send(new GetCartorioWorkflowAgentExecution(executionId,
            parsedAgentId!, version, scope.AgentIds.Select(id => new AgentId(id)).ToFrozenSet()),
            cancellationToken);
        return execution is null ? NotFound() : Ok(ToResponse(execution));
    }

    private static bool TryBuildOrigin(CartorioWorkflowAgentExecutionRequest request,
        out TrustedExecutionOrigin? origin)
    {
        origin = null;
        if (request.WorkflowId == Guid.Empty || request.WorkflowVersion < 1 ||
            request.RunId == Guid.Empty || request.ConversationId == Guid.Empty ||
            request.SolicitacaoId == Guid.Empty || !ValidIdentifier(request.StepId, 120) ||
            request.Originator is null)
            return false;

        string actorId;
        if (string.Equals(request.Originator.Kind, "system", StringComparison.Ordinal))
        {
            if (!string.IsNullOrWhiteSpace(request.Originator.ActorId)) return false;
            actorId = WorkflowOrigin;
        }
        else if (string.Equals(request.Originator.Kind, "operator", StringComparison.Ordinal))
        {
            if (!ValidIdentifier(request.Originator.ActorId, 128)) return false;
            actorId = request.Originator.ActorId!;
        }
        else return false;

        origin = new(WorkflowOrigin, request.Originator.Kind, actorId, request.WorkflowId,
            request.WorkflowVersion, request.RunId, request.StepId, request.ConversationId,
            request.SolicitacaoId);
        return true;
    }

    private static bool TryAgentId(string value, out AgentId? agentId)
    {
        try { agentId = new(value); return true; }
        catch (ArgumentException) { agentId = null; return false; }
    }

    private static bool ValidIdentifier(string? value, int max) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= max &&
        Regex.IsMatch(value, @"^[a-zA-Z0-9._:@/-]+$");

    private static HarnessResponse Failed(string correlationId, string code) =>
        new(Guid.Empty, correlationId ?? string.Empty, null, null, "Failed", null,
            new(code, "The execution was rejected."));

    private static int StatusFor(HarnessResponse response) => response.Error?.Code switch
    {
        "invalid_request" => 400,
        "tenant_forbidden" or "execution_forbidden" or "agent_disabled" or
            "agent_channel_forbidden" or "tool_forbidden" or "tool_resource_forbidden" => 403,
        "agent_not_found" or "tenant_agent_not_configured" => 404,
        "agent_version_conflict" or "execution_conflict" => 409,
        "execution_timed_out" or "tool_timed_out" => 504,
        "provider_unavailable" or "model_route_unavailable" or "memory_not_configured" => 503,
        _ => 422
    };

    private static HarnessResponse ToResponse(ExecutionSnapshot snapshot) =>
        new(snapshot.Context.ExecutionId.Value, snapshot.Context.CorrelationId,
            snapshot.Context.AgentId.Value, snapshot.Context.AgentVersion.Value,
            snapshot.Status.ToString(), snapshot.ResponseContent,
            snapshot.ErrorCode is null ? null : new(snapshot.ErrorCode,
                "The execution did not complete."), snapshot.ProviderId, snapshot.ModelId,
            snapshot.InputTokens, snapshot.OutputTokens, snapshot.EstimatedCost);
}
