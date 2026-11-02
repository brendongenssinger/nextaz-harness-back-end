using MediatR;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Runtime.ModelRouting;
namespace NextAZ.Harness.Application.Executions;
public sealed record ExecuteHarnessRequest(HarnessRequest Request, ResolvedExecutionProfile? Profile = null,
    IReadOnlyDictionary<string, string>? ResourceReferences = null,
    TrustedExecutionOrigin? TrustedOrigin = null,
    IReadOnlyList<ModelMediaInput>? MediaInputs = null) : IRequest<HarnessResponse>;
