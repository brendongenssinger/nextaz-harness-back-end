using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Api.Configuration;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Contracts.Administration;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tenants/{tenantId:guid}/overview")]
[Authorize(Policy = "HarnessRead")]
public sealed class HarnessAdministrationController(
    ICurrentTenant currentTenant,
    IAgentRegistry agents,
    ITenantAgentConfigurationRepository configurations,
    IModelRouter modelRouter,
    HarnessRuntimeDescriptor runtime) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HarnessAdministrationOverview>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HarnessAdministrationOverview>> GetAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || currentTenant.TenantId != tenantId)
            return Forbid();

        var definitions = await agents.ListAsync(cancellationToken);
        var tenantConfigurations = await configurations.ListAsync(tenantId, cancellationToken);
        var byAgent = tenantConfigurations.ToDictionary(configuration => configuration.AgentId);
        var rows = new List<HarnessAdministrationAgent>(definitions.Count);

        foreach (var definition in definitions.OrderBy(agent => agent.AgentId.Value, StringComparer.Ordinal))
        {
            byAgent.TryGetValue(definition.AgentId, out var configuration);
            var routes = configuration is null
                ? []
                : await modelRouter.ResolveAsync(tenantId, definition.AgentId, definition.Version,
                    configuration.ModelPolicy, cancellationToken);
            rows.Add(new HarnessAdministrationAgent(
                definition.AgentId.Value,
                definition.Version.Value,
                definition.Name,
                definition.Vertical,
                definition.Objective,
                definition.Enabled,
                definition.RequiredPolicies.Select(value => value).Order(StringComparer.Ordinal).ToArray(),
                definition.AllowedTools.Select(tool => tool.Value).Order(StringComparer.Ordinal).ToArray(),
                configuration is not null,
                configuration?.Enabled ?? false,
                configuration?.ConfigurationVersion,
                configuration?.Instructions,
                configuration?.ModelPolicy.Name,
                configuration?.MemoryPolicy.Name,
                configuration?.AllowedTools.Select(tool => tool.Value).Order(StringComparer.Ordinal).ToArray() ?? [],
                routes.Select(route => new HarnessAdministrationModelRoute(
                    route.ProviderId,
                    route.ModelId,
                    Convert.ToInt32(route.Timeout.TotalSeconds))).ToArray(),
                configuration is null
                    ? null
                    : new HarnessAdministrationExecutionLimits(
                        configuration.Limits.MaxSteps,
                        configuration.Limits.MaxToolCalls,
                        configuration.Limits.MaxOutputTokens,
                        Convert.ToInt32(configuration.Limits.Timeout.TotalSeconds))));
        }

        return Ok(new HarnessAdministrationOverview(
            tenantId,
            runtime.Mode,
            runtime.Persistence,
            runtime.Provider,
            runtime.Messaging,
            rows));
    }
}
