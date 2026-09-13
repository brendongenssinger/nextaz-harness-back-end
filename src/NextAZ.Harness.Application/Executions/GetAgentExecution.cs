using MediatR;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Application.Executions;
public sealed record GetAgentExecution(Guid ExecutionId, AgentId AgentId) : IRequest<ExecutionSnapshot?>;
