using NextAZ.Harness.Domain.Executions;
namespace NextAZ.Harness.Application.Abstractions;
public interface IExecutionTelemetry
{
    void Record(ExecutionSnapshot execution);
}
