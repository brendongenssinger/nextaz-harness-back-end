namespace NextAZ.Harness.Runtime.Abstractions;
public interface IModelRouter
{
    Task<IReadOnlyList<ModelRouting.ModelRoute>> ResolveAsync(Guid tenantId, ModelPolicy policy, CancellationToken cancellationToken);
}
