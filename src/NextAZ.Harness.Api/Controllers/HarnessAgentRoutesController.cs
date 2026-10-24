using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Administration;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tenants/{tenantId:guid}/routes")]
[Authorize(Policy = "HarnessManage")]
public sealed class HarnessAgentRoutesController(
    ICurrentTenant currentTenant,
    IAgentRegistry agents,
    ITenantAgentConfigurationRepository configurations,
    IAgentRouteBindingRepository bindings) : ControllerBase
{
    [HttpPut]
    public async Task<ActionResult<HarnessAdministrationRouteBinding>> SaveAsync(
        Guid tenantId,
        SaveHarnessAdministrationRouteBinding request,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || currentTenant.TenantId != tenantId)
            return Forbid();

        AgentId agentId;
        AgentVersion agentVersion;
        try
        {
            AgentRouteBinding.ValidateRoutePart(request.Channel, 32, nameof(request.Channel));
            AgentRouteBinding.ValidateRoutePart(request.Operation, 120, nameof(request.Operation));
            agentId = new AgentId(request.AgentId);
            agentVersion = new AgentVersion(request.AgentVersion);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ProblemDetails { Title = "Invalid agent route.", Detail = exception.Message });
        }

        var agent = await agents.GetAsync(agentId, cancellationToken);
        var configuration = await configurations.GetAsync(tenantId, agentId, cancellationToken);
        if (agent is null || configuration is null || !agent.Enabled || !configuration.Enabled ||
            agent.Version != agentVersion)
            return UnprocessableEntity(new ProblemDetails
            {
                Title = "The selected published agent version is not available for this tenant."
            });

        var serviceActor = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(serviceActor) || serviceActor.Length > 120)
            return Forbid();

        try
        {
            var saved = await bindings.SaveAsync(new AgentRouteBindingUpdate(
                tenantId, request.Channel, request.Operation, agentId, agentVersion,
                request.Enabled, request.ExpectedRevision, serviceActor), cancellationToken);
            return Ok(HarnessAdministrationController.ToContract(saved));
        }
        catch (AgentRouteRevisionConflictException)
        {
            return Conflict(new ProblemDetails
            {
                Title = "The route was changed by another administrator. Reload before saving."
            });
        }
    }
}
