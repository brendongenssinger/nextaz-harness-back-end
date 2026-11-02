using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Administration;
using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.Api.Controllers;

[ApiController]
[Route("api/v1/admin/tenants/{tenantId:guid}/usage")]
[Authorize(Policy = "HarnessRead")]
public sealed class HarnessUsageController(
    ICurrentTenant currentTenant,
    IHarnessUsageReader usageReader,
    IConfiguration configuration,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HarnessUsageReport>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<HarnessUsageReport>> GetAsync(
        Guid tenantId,
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || tenantId != currentTenant.TenantId)
            return Forbid();

        var to = toUtc ?? timeProvider.GetUtcNow();
        var from = fromUtc ?? to.AddDays(-30);
        if (from >= to || to - from > TimeSpan.FromDays(93))
            return ValidationProblem("O período deve ser positivo e limitado a 93 dias.");

        var currency = configuration["Harness:Costs:Currency"]?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency)) currency = "USD";
        if (currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            return Problem(statusCode: 500, title: "Harness cost currency is invalid.");

        return Ok(await usageReader.ReadAsync(
            tenantId, from, to, currency, cancellationToken));
    }
}
