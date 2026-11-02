using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Api.Configuration;

public sealed class HarnessRouteReadiness(IEnumerable<IModelProvider> providers)
{
    private readonly IReadOnlySet<string> _providerIds = providers
        .Select(provider => provider.ProviderId)
        .ToHashSet(StringComparer.Ordinal);

    public bool AreOperational(IReadOnlyList<ModelRoute> routes) =>
        routes.Count > 0 && routes.All(route =>
            route.ProviderConnectionId is not null && _providerIds.Contains(route.ProviderId));
}
