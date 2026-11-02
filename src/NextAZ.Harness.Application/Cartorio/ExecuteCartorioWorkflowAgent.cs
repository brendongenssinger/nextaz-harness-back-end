using MediatR;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Domain.Agents;

namespace NextAZ.Harness.Application.Cartorio;

public sealed record ExecuteCartorioWorkflowAgent(AgentId AgentId, int AgentVersion,
    IReadOnlySet<string> AllowedChannels, IReadOnlySet<AgentId> AllowedAgentIds,
    CartorioWorkflowAgentExecutionRequest Request,
    TrustedExecutionOrigin TrustedOrigin, bool ResponseOnly = false) : IRequest<HarnessResponse>;
