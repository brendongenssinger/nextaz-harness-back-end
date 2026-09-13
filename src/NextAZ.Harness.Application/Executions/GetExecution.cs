using MediatR;
using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Application.Executions;
public sealed record GetExecution(Guid ExecutionId) : IRequest<ExecutionSnapshot?>;
