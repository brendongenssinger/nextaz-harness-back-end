using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Imobiliaria;
using NextAZ.Harness.Contracts.Imobiliaria;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/imobiliaria/vitrine/busca-assistida")]
public sealed class ImobiliariaVitrineBuscaAssistidaController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "ImobiliariaExecute")]
    [RequestSizeLimit(16_384)]
    public async Task<ActionResult<BuscaAssistidaResponse>> Execute(BuscaAssistidaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new InterpretarBuscaAssistida(request), cancellationToken);
        if (result.Response is not null) return Ok(result.Response);
        return StatusCode(StatusFor(result.ErrorCode), new ProblemDetails
        {
            Status = StatusFor(result.ErrorCode),
            Title = "Assisted search request failed.",
            Extensions = { ["code"] = result.ErrorCode ?? "execution_failed" }
        });
    }

    private static int StatusFor(string? errorCode) => errorCode switch
    {
        "invalid_request" => StatusCodes.Status400BadRequest,
        "tenant_forbidden" or "execution_forbidden" => StatusCodes.Status403Forbidden,
        "model_output_invalid" or "provider_request_rejected" or "provider_response_incomplete" or
            "provider_response_failed" => StatusCodes.Status502BadGateway,
        "busca_assistida_unavailable" or "provider_unavailable" or "provider_not_configured" or
            "model_route_unavailable" or "execution_timed_out" => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status503ServiceUnavailable
    };
}
