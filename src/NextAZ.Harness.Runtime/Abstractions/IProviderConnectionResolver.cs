using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Runtime.Abstractions;

public interface IProviderConnectionResolver
{
    Task<ProviderConnectionCredentials> ResolveAsync(Guid tenantId, Guid providerConnectionId,
        CancellationToken cancellationToken);
}
