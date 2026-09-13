using NextAZ.Harness.Runtime.Execution;
namespace NextAZ.Harness.Runtime.Policies;
public sealed class ExecutionPolicyEvaluator : IExecutionPolicyEvaluator
{
    public void Validate(HarnessContext context, AgentDefinition agent, TenantAgentConfiguration configuration,
        IReadOnlySet<string> permissions)
    {
        if (context.TenantId == Guid.Empty || configuration.TenantId != context.TenantId ||
            agent.AgentId != context.AgentId || configuration.AgentId != agent.AgentId ||
            agent.Version != context.AgentVersion)
            throw new HarnessExecutionException("tenant_context_mismatch");
        if (!agent.Enabled || !configuration.Enabled)
            throw new HarnessExecutionException("agent_disabled");
        if (configuration.ConfigurationVersion < 1)
            throw new HarnessExecutionException("invalid_configuration");
        if (!permissions.Contains("harness.execute"))
            throw new HarnessExecutionException("execution_forbidden");
        foreach (var policy in agent.RequiredPolicies)
            if (policy != "tenant-isolation" && policy != "tools-allowlist")
                throw new HarnessExecutionException("unsupported_policy");
    }
}
