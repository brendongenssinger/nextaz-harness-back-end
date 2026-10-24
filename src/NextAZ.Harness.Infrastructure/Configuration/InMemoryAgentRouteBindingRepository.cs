using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Runtime.Abstractions;

namespace NextAZ.Harness.Infrastructure.Configuration;

public sealed class InMemoryAgentRouteBindingRepository(TimeProvider timeProvider)
    : IAgentRouteBindingRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid TenantId, string Channel, string Operation), AgentRouteBinding> _bindings = [];

    public Task<AgentRouteBinding?> GetAsync(Guid tenantId, string channel, string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            return Task.FromResult(_bindings.GetValueOrDefault((tenantId, channel, operation)));
    }

    public Task<IReadOnlyList<AgentRouteBinding>> ListAsync(Guid tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
            return Task.FromResult<IReadOnlyList<AgentRouteBinding>>(_bindings.Values
                .Where(binding => binding.TenantId == tenantId)
                .OrderBy(binding => binding.Channel, StringComparer.Ordinal)
                .ThenBy(binding => binding.Operation, StringComparer.Ordinal)
                .ToArray());
    }

    public Task<AgentRouteBinding> SaveAsync(AgentRouteBindingUpdate update,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            var key = (update.TenantId, update.Channel, update.Operation);
            _bindings.TryGetValue(key, out var current);
            if ((current is null && update.ExpectedRevision is not null) ||
                (current is not null && update.ExpectedRevision != current.Revision))
                throw new AgentRouteRevisionConflictException();

            var binding = new AgentRouteBinding(update.TenantId, update.Channel, update.Operation,
                update.AgentId, update.AgentVersion, update.Enabled, (current?.Revision ?? 0) + 1,
                timeProvider.GetUtcNow(), update.UpdatedBy);
            _bindings[key] = binding;
            return Task.FromResult(binding);
        }
    }
}
