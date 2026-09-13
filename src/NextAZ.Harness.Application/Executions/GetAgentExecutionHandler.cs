using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Application.Executions;
public sealed class GetAgentExecutionHandler(ICurrentTenant tenant, IExecutionRepository executions)
    : IRequestHandler<GetAgentExecution, ExecutionSnapshot?>
{
    public async Task<ExecutionSnapshot?> Handle(GetAgentExecution request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty || !tenant.Permissions.Contains("harness.read"))
            throw new UnauthorizedAccessException();
        var execution = await executions.GetAsync(tenant.TenantId, request.ExecutionId, cancellationToken);
        return execution?.Context.AgentId == request.AgentId ? execution : null;
    }
}
