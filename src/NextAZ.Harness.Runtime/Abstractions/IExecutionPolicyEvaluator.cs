namespace NextAZ.Harness.Runtime.Abstractions;
public interface IExecutionPolicyEvaluator
{
    void Validate(HarnessContext context, AgentDefinition agent, TenantAgentConfiguration configuration, IReadOnlySet<string> permissions);
}
