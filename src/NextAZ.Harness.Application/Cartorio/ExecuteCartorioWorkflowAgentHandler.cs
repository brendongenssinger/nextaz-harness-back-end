using MediatR;
using NextAZ.Harness.Application.Abstractions;
using NextAZ.Harness.Application.Executions;
using NextAZ.Harness.Contracts.Executions;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Application.Cartorio;

public sealed class ExecuteCartorioWorkflowAgentHandler(ICurrentTenant tenant, IAgentRegistry agents,
    ITenantAgentConfigurationRepository configurations, ISender sender)
    : IRequestHandler<ExecuteCartorioWorkflowAgent, HarnessResponse>
{
    public async Task<HarnessResponse> Handle(ExecuteCartorioWorkflowAgent command,
        CancellationToken cancellationToken)
    {
        HarnessResponse Reject(string code) => new(Guid.Empty, command.Request.CorrelationId,
            null, null, "Failed", null, new(code, "The execution was rejected."));
        if (tenant.TenantId == Guid.Empty) return Reject("tenant_forbidden");
        if (!tenant.Permissions.Contains("harness.execute")) return Reject("execution_forbidden");
        if (!command.AllowedAgentIds.Contains(command.AgentId)) return Reject("agent_not_found");
        if (!command.AllowedChannels.Contains(command.Request.Channel))
            return Reject("agent_channel_forbidden");

        var agent = await agents.GetAsync(command.AgentId, cancellationToken);
        if (agent is null || !string.Equals(agent.Vertical, "cartorio", StringComparison.Ordinal))
            return Reject("agent_not_found");
        if (agent.Version.Value != command.AgentVersion)
            return Reject("agent_version_conflict");

        var configuration = await configurations.GetAsync(tenant.TenantId, command.AgentId,
            cancellationToken);
        if (configuration is null) return Reject("tenant_agent_not_configured");
        if (!agent.Enabled || !configuration.Enabled) return Reject("agent_disabled");

        var request = new HarnessRequest(tenant.TenantId, command.Request.Channel,
            command.ResponseOnly ? "cartorio.workflow.response.generate" : "cartorio.workflow.agent.execute", command.Request.Content,
            command.Request.CorrelationId);
        var references = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["conversationId"] = command.Request.ConversationId.ToString("D")
        };
        if (command.Request.SolicitacaoId is { } solicitacaoId)
            references["solicitacaoId"] = solicitacaoId.ToString("D");
        var profile = new ResolvedExecutionProfile(agent, configuration, [],
            DisableTools: command.ResponseOnly, SimulationOnly: false);
        return await sender.Send(new ExecuteHarnessRequest(request, profile, references,
            command.TrustedOrigin), cancellationToken);
    }
}
