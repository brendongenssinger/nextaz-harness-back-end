namespace NextAZ.Harness.Runtime.ModelRouting;
public sealed class ConfiguredModelRouter(
    IReadOnlyDictionary<(Guid TenantId, string Policy), IReadOnlyList<ModelRoute>> routes) : IModelRouter
{
    public Task<IReadOnlyList<ModelRoute>> ResolveAsync(Guid tenantId, ModelPolicy policy, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant required.", nameof(tenantId));
        return Task.FromResult(routes.TryGetValue((tenantId, policy.Name), out var result)
            ? (IReadOnlyList<ModelRoute>)Array.AsReadOnly(result.ToArray()) : Array.Empty<ModelRoute>());
    }
}
