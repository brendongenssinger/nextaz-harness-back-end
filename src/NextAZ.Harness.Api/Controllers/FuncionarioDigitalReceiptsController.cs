using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/funcionario-digital/receipts")]
public sealed class FuncionarioDigitalReceiptsController(ISender sender) : ControllerBase
{
    private static readonly AgentId AgentId = new("funcionario-digital.recibo");

    [HttpPost]
    [Authorize(Policy = "FuncionarioDigitalReceiptsExecute")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<ActionResult<HarnessResponse>> Execute(HarnessRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Operation, AgentId.Value, StringComparison.Ordinal))
            return BadRequest(Failed(request, "invalid_operation", "Only funcionario-digital.recibo is accepted."));
        if (!TryResolveReferences(request.Metadata, out var references))
            return BadRequest(Failed(request, "invalid_request", "Valid receipt resource references are required."));

        var result = await sender.Send(new ExecuteHarnessRequest(request, ResourceReferences: references), cancellationToken);
        if (result.Status == "Completed")
            return CreatedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        if (result.Status is "Pending" or "Running")
            return AcceptedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        return StatusCode(StatusFor(result), result);
    }

    [HttpGet("{executionId:guid}")]
    [Authorize(Policy = "FuncionarioDigitalReceiptsRead")]
    public async Task<ActionResult<HarnessResponse>> Get(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await sender.Send(new GetAgentExecution(executionId, AgentId), cancellationToken);
        return execution is null ? NotFound() : Ok(ToResponse(execution));
    }

    private static HarnessResponse Failed(HarnessRequest request, string code, string message) =>
        new(Guid.Empty, request.CorrelationId, null, null, "Failed", null, new(code, message));

    private static int StatusFor(HarnessResponse response) => response.Error?.Code switch
    {
        "invalid_request" or "invalid_operation" => 400,
        "tenant_forbidden" or "execution_forbidden" or "agent_disabled" or "tool_forbidden" => 403,
        "agent_not_found" or "tenant_agent_not_configured" => 404,
        "execution_timed_out" => 504,
        "provider_unavailable" or "model_route_unavailable" => 503,
        "execution_conflict" => 409,
        _ => 422
    };

    private static HarnessResponse ToResponse(ExecutionSnapshot snapshot) =>
        new(snapshot.Context.ExecutionId.Value, snapshot.Context.CorrelationId,
            snapshot.Context.AgentId.Value, snapshot.Context.AgentVersion.Value,
            snapshot.Status.ToString(), snapshot.ResponseContent,
            snapshot.ErrorCode is null ? null : new(snapshot.ErrorCode, "The execution did not complete."),
            snapshot.ProviderId, snapshot.ModelId, snapshot.InputTokens, snapshot.OutputTokens,
            snapshot.EstimatedCost);

    private static bool TryResolveReferences(IReadOnlyDictionary<string, string>? metadata,
        out IReadOnlyDictionary<string, string> references)
    {
        references = new Dictionary<string, string>();
        if (metadata is null || metadata.Keys.Any(key => key is not ("source" or "atendimentoId" or "messageId")) ||
            !metadata.TryGetValue("source", out var source) || source != "nextaz-funcionario-digital" ||
            !TryReadGuid(metadata, "atendimentoId", out var atendimentoId) ||
            !TryReadGuid(metadata, "messageId", out var messageId))
            return false;
        references = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["atendimentoId"] = atendimentoId.ToString("D"),
            ["messageId"] = messageId.ToString("D")
        };
        return true;
    }

    private static bool TryReadGuid(IReadOnlyDictionary<string, string> metadata, string key, out Guid value)
    {
        value = Guid.Empty;
        return metadata.TryGetValue(key, out var raw) && Guid.TryParse(raw, out value) && value != Guid.Empty;
    }
}
