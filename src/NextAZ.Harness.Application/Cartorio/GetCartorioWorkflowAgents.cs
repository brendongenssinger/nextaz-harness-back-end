using MediatR;
using NextAZ.Harness.Contracts.Cartorio;
using NextAZ.Harness.Domain.Agents;

namespace NextAZ.Harness.Application.Cartorio;

public sealed record GetCartorioWorkflowAgents(IReadOnlyList<string> Channels,
    IReadOnlySet<AgentId> AllowedAgentIds)
    : IRequest<CartorioWorkflowAgentCatalogResponse>;
