using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Api.Controllers;
[ApiController]
[Route("api/v1/imobiliaria/atendimentos")]
public sealed class ImobiliariaAtendimentosController(ISender sender) : ControllerBase
{
    private static readonly AgentId AgentId = new("imobiliaria.atendimento");
    [HttpPost]
    [Authorize(Policy = "ImobiliariaExecute")]
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<HarnessResponse>> Execute(HarnessRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Operation, "imobiliaria.atendimento", StringComparison.Ordinal))
            return BadRequest(new HarnessResponse(Guid.Empty, request.CorrelationId, null, null, "Failed",
                null, new("invalid_operation", "Only imobiliaria.atendimento is accepted.")));
        if (!TryResolveReferences(request.Metadata, out var references))
            return BadRequest(new HarnessResponse(Guid.Empty, request.CorrelationId, null, null, "Failed",
                null, new("invalid_request", "Valid real-estate resource references are required.")));
        var result = await sender.Send(new ExecuteHarnessRequest(request, ResourceReferences: references), cancellationToken);
        if (result.Status == "Completed")
            return CreatedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        if (result.Status is "Pending" or "Running")
            return AcceptedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        return StatusCode(StatusFor(result), result);
    }
    [HttpGet("{executionId:guid}")]
    [Authorize(Policy = "ImobiliariaRead")]
    public async Task<ActionResult<HarnessResponse>> Get(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await sender.Send(new GetAgentExecution(executionId, AgentId), cancellationToken);
        return execution is null ? NotFound() : Ok(ToResponse(execution));
    }
    private static int StatusFor(HarnessResponse response) => response.Error?.Code switch
    {
        "invalid_request" or "invalid_operation" => 400,
        "tenant_forbidden" or "execution_forbidden" or "agent_disabled" or "tool_forbidden" or
            "tool_resource_forbidden" => 403,
        "agent_not_found" or "tenant_agent_not_configured" => 404,
        "execution_timed_out" or "tool_timed_out" => 504,
        "provider_unavailable" or "model_route_unavailable" or "memory_not_configured" => 503,
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
        if (metadata is null || metadata.Keys.Any(key => key is not ("source" or "atendimentoId" or "contatoId")) ||
            !metadata.TryGetValue("source", out var source) || source != "nextaz-imobiliaria" ||
            !metadata.TryGetValue("atendimentoId", out var atendimento) ||
            !Guid.TryParse(atendimento, out var atendimentoId) || atendimentoId == Guid.Empty)
            return false;
        var result = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["atendimentoId"] = atendimentoId.ToString()
        };
        if (metadata.TryGetValue("contatoId", out var contato))
        {
            if (!Guid.TryParse(contato, out var contatoId) || contatoId == Guid.Empty) return false;
            result["contatoId"] = contatoId.ToString();
        }
        references = result;
        return true;
    }
}
