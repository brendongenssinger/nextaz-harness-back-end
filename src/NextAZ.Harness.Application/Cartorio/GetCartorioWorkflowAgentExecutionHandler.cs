using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Application.Cartorio;

public sealed class GetCartorioWorkflowAgentExecutionHandler(ICurrentTenant tenant,
    IExecutionRepository executions, IAgentRegistry agents)
    : IRequestHandler<GetCartorioWorkflowAgentExecution, ExecutionSnapshot?>
{
    private const string WorkflowOperation = "cartorio.workflow.agent.execute";

    public async Task<ExecutionSnapshot?> Handle(GetCartorioWorkflowAgentExecution request,
        CancellationToken cancellationToken)
    {
        if (tenant.TenantId == Guid.Empty || !tenant.Permissions.Contains("harness.read"))
            throw new UnauthorizedAccessException();
        if (request.AgentVersion < 1 || !request.AllowedAgentIds.Contains(request.AgentId)) return null;

        var agent = await agents.GetAsync(request.AgentId, cancellationToken);
        if (agent is null || !string.Equals(agent.Vertical, "cartorio", StringComparison.Ordinal))
            return null;

        var execution = await executions.GetAsync(tenant.TenantId, request.ExecutionId,
            cancellationToken);
        return execution?.Context.AgentId == request.AgentId &&
            execution.Context.AgentVersion.Value == request.AgentVersion &&
            (string.Equals(execution.Context.Operation, WorkflowOperation, StringComparison.Ordinal) || execution.Context.Operation == "cartorio.workflow.response.generate")
            ? execution
            : null;
    }
}
