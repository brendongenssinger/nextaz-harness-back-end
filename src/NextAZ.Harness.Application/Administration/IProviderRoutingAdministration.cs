namespace NextAZ.Harness.Application.Administration;

public sealed record ProviderConnectionDraft(string ConnectionKey, string ProviderId, string DisplayName,
    string Endpoint, string ApiKey, bool Enabled);

public sealed record ProviderConnectionView(Guid ProviderConnectionId, string ConnectionKey,
    string ProviderId, string DisplayName, string Endpoint, bool Enabled);

public sealed record AgentModelRouteDraft(Guid ProviderConnectionId, string ModelPolicy, string ModelId,
    int TimeoutSeconds, bool Enabled);

public interface IProviderRoutingAdministration
{
    Task<ProviderConnectionView> UpsertConnectionAsync(Guid tenantId, ProviderConnectionDraft draft,
        CancellationToken cancellationToken);
    Task ReplaceAgentRoutesAsync(Guid tenantId, string agentId, int agentVersion,
        IReadOnlyList<AgentModelRouteDraft> routes, CancellationToken cancellationToken);
}
