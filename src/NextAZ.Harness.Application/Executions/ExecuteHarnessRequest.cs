using MediatR;
using NextAZ.Harness.Contracts.Executions;
namespace NextAZ.Harness.Application.Executions;
public sealed record ExecuteHarnessRequest(HarnessRequest Request, ResolvedExecutionProfile? Profile = null,
    IReadOnlyDictionary<string, string>? ResourceReferences = null) : IRequest<HarnessResponse>;
