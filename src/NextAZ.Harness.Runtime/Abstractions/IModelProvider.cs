namespace NextAZ.Harness.Runtime.Abstractions;
public interface IModelProvider
{
    string ProviderId { get; }
    Task<ModelRouting.ModelExecutionResult> ExecuteAsync(ModelRouting.ModelExecutionRequest request, CancellationToken cancellationToken);
}
