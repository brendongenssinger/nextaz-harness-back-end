using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Application.Executions;
public sealed class GetExecutionHandler(ICurrentTenant tenant, IExecutionRepository executions)
    : IRequestHandler<GetExecution, ExecutionSnapshot?>
{
    public Task<ExecutionSnapshot?> Handle(GetExecution request, CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty || !tenant.Permissions.Contains("harness.read"))
            throw new UnauthorizedAccessException();
        return executions.GetAsync(tenant.TenantId, request.ExecutionId, cancellationToken);
    }
}
