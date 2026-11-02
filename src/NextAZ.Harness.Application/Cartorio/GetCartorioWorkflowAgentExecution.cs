using MediatR;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;

namespace NextAZ.Harness.Application.Cartorio;

public sealed record GetCartorioWorkflowAgentExecution(Guid ExecutionId, AgentId AgentId,
    int AgentVersion, IReadOnlySet<AgentId> AllowedAgentIds) : IRequest<ExecutionSnapshot?>;
