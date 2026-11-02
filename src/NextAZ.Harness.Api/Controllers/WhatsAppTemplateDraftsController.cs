using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.WhatsAppTemplates;
using NextAZ.Harness.Contracts.WhatsAppTemplates;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/whatsapp-template-drafts")]
[Authorize(Policy = "WhatsAppTemplateGenerate")]
public sealed class WhatsAppTemplateDraftsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(32768)]
    public async Task<ActionResult<WhatsAppTemplateDraftResponse>> Generate(
        GenerateWhatsAppTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateWhatsAppTemplate(request), cancellationToken);
        return result.Status == "Completed" ? Ok(result) : StatusCode(result.Error?.Code switch
        {
            "invalid_request" => 400,
            "tenant_forbidden" or "execution_forbidden" or "agent_disabled" => 403,
            "agent_not_found" or "tenant_agent_not_configured" => 404,
            "execution_timed_out" => 504,
            "provider_unavailable" or "model_route_unavailable" => 503,
            "execution_conflict" => 409,
            _ => 422
        }, result);
    }
}
