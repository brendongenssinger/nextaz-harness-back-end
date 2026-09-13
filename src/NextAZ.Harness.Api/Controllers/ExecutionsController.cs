using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Api.Controllers;
[ApiController]
[Route("api/v1/executions")]
public sealed class ExecutionsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "HarnessExecute")]
    [RequestSizeLimit(65536)]
    public async Task<ActionResult<HarnessResponse>> Execute(HarnessRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ExecuteHarnessRequest(request), cancellationToken);
        if (result.Status == "Completed")
            return CreatedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        if (result.Status is "Pending" or "Running")
            return AcceptedAtAction(nameof(Get), new { executionId = result.ExecutionId }, result);
        var status = result.Error?.Code switch
        {
            "invalid_request" => 400,
            "tenant_forbidden" or "execution_forbidden" or "agent_disabled" or "tool_forbidden" => 403,
            "agent_not_found" or "tenant_agent_not_configured" => 404,
            "execution_timed_out" or "tool_timed_out" => 504,
            "provider_unavailable" or "model_route_unavailable" or "memory_not_configured" => 503,
            "execution_conflict" => 409,
            _ => 422
        };
        return StatusCode(status, result);
    }
    [HttpGet("{executionId:guid}")]
    [Authorize(Policy = "HarnessRead")]
    public async Task<ActionResult<ExecutionSnapshot>> Get(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await sender.Send(new GetExecution(executionId), cancellationToken);
        return execution is null ? NotFound() : Ok(execution);
    }
}
