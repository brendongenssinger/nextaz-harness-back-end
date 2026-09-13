namespace NextAZ.Harness.Runtime.Abstractions;
public interface IAgentRuntime
{
    Task<Execution.AgentExecutionResult> ExecuteAsync(Execution.AgentExecutionRequest request, CancellationToken cancellationToken);
}
