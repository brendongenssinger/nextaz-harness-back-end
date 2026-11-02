using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Receipts;
using NextAZ.Harness.Contracts.Receipts;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/receipt-classifications")]
[Authorize(Policy = "ReceiptClassify")]
public sealed class ReceiptClassificationsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(16384)]
    public async Task<ActionResult<ReceiptClassificationResponse>> Classify(
        ClassifyReceiptRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ClassifyReceipt(request), cancellationToken);
        return result.Status == "Completed" ? Ok(result) : StatusCode(result.Error?.Code switch
        {
            "invalid_request" => 400,
            "tenant_forbidden" or "execution_forbidden" or "agent_disabled" or
                "media_source_forbidden" => 403,
            "agent_not_found" or "tenant_agent_not_configured" => 404,
            "execution_timed_out" => 504,
            "provider_unavailable" or "model_route_unavailable" or "media_unavailable" => 503,
            "execution_conflict" => 409,
            "media_too_large" => 413,
            "media_type_unsupported" => 415,
            _ => 422
        }, result);
    }
}
