namespace NextAZ.Harness.Runtime.Abstractions;
public interface IToolExecutor
{
    Task<Tooling.ToolResult> ExecuteAsync(Execution.AgentExecutionRequest execution, Tooling.ToolCall call, CancellationToken cancellationToken);
}
