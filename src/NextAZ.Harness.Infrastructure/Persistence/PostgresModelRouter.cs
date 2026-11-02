using Microsoft.EntityFrameworkCore;
using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Policies;
using NextAZ.Harness.Runtime.Abstractions;
using NextAZ.Harness.Runtime.ModelRouting;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class PostgresModelRouter(IDbContextFactory<HarnessDbContext> contextFactory) : IModelRouter
{
    public async Task<IReadOnlyList<ModelRoute>> ResolveAsync(Guid tenantId, AgentId agentId,
        AgentVersion agentVersion, ModelPolicy policy, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant required.", nameof(tenantId));
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AgentModelRoutes.AsNoTracking()
            .Where(route => route.TenantId == tenantId &&
                route.AgentId == agentId.Value &&
                route.AgentVersion == agentVersion.Value &&
                route.ModelPolicy == policy.Name &&
                route.Enabled && route.ProviderConnection.Enabled)
            .OrderBy(route => route.Priority)
            .Select(route => new ModelRoute(route.ProviderConnection.ProviderId, route.ModelId,
                TimeSpan.FromSeconds(route.TimeoutSeconds), route.ProviderConnectionId))
            .ToArrayAsync(cancellationToken);
    }
}
