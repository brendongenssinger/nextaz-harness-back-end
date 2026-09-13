using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Employees;
using NextAZ.Harness.Contracts.Employees;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.Api.Controllers;
[ApiController]
[Route("api/v1/employee-drafts")]
[Authorize(Policy = "HarnessEmployeeDraft")]
public sealed class EmployeeDraftsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(262144)]
    public async Task<ActionResult<HarnessResponse>> Generate(EmployeeDraftRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GenerateEmployeeDraft(request), cancellationToken);
        return result.Status == "Completed" ? Ok(result) : StatusCode(result.Error?.Code switch {
            "invalid_request" => 400,
            "tenant_forbidden" or "agent_disabled" => 403,
            "employee_profile_not_found" => 404,
            "execution_timed_out" => 504,
            _ => 422
        }, result);
    }
}
