using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Administration;
using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tenants/{tenantId:guid}")]
[Authorize(Policy = "HarnessWrite")]
public sealed class ProviderRoutingAdministrationController(
    ICurrentTenant currentTenant,
    IProviderRoutingAdministration administration) : ControllerBase
{
    [HttpPut("provider-connections/{connectionKey}")]
    [ProducesResponseType<ProviderConnectionResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProviderConnectionResponse>> UpsertConnectionAsync(Guid tenantId,
        string connectionKey, [FromBody] UpsertProviderConnectionRequest request,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || currentTenant.TenantId != tenantId) return Forbid();
        try
        {
            var result = await administration.UpsertConnectionAsync(tenantId,
                new(connectionKey, request.ProviderId, request.DisplayName, request.Endpoint,
                    request.ApiKey, request.Enabled), cancellationToken);
            return Ok(new ProviderConnectionResponse(result.ProviderConnectionId, result.ConnectionKey,
                result.ProviderId, result.DisplayName, result.Endpoint, result.Enabled));
        }
        catch (ArgumentException)
        {
            return ValidationProblem("The provider connection is invalid.");
        }
        catch (InvalidOperationException exception) when
            (exception.Message is "provider_connection_identity_conflict" or
             "provider_connection_identity_immutable")
        {
            return Conflict(new { code = exception.Message });
        }
    }

    [HttpPut("agents/{agentId}/versions/{agentVersion:int}/model-routes")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReplaceRoutesAsync(Guid tenantId, string agentId, int agentVersion,
        [FromBody] ReplaceAgentModelRoutesRequest request, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || currentTenant.TenantId != tenantId) return Forbid();
        try
        {
            await administration.ReplaceAgentRoutesAsync(tenantId, agentId, agentVersion,
                request.Routes.Select(route => new AgentModelRouteDraft(route.ProviderConnectionId,
                    route.ModelPolicy, route.ModelId, route.TimeoutSeconds, route.Enabled)).ToArray(),
                cancellationToken);
            return NoContent();
        }
        catch (ArgumentException)
        {
            return ValidationProblem("The model route chain is invalid.");
        }
        catch (InvalidOperationException exception) when
            (exception.Message is "provider_connection_not_available" or "agent_model_route_immutable")
        {
            return Conflict(new { code = exception.Message });
        }
    }
}
