using MediatR;
using NextAZ.Harness.Contracts.Employees;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.Application.Employees;
public sealed record GenerateEmployeeDraft(EmployeeDraftRequest Request) : IRequest<HarnessResponse>;
