namespace NextAZ.Harness.Contracts.Administration;

public sealed record UpsertProviderConnectionRequest(string ProviderId, string DisplayName,
    string Endpoint, string ApiKey, bool Enabled = true);

public sealed record ProviderConnectionResponse(Guid ProviderConnectionId, string ConnectionKey,
    string ProviderId, string DisplayName, string Endpoint, bool Enabled);

public sealed record ReplaceAgentModelRoutesRequest(IReadOnlyList<AgentModelRouteRequest> Routes);

public sealed record AgentModelRouteRequest(Guid ProviderConnectionId, string ModelPolicy,
    string ModelId, int TimeoutSeconds, bool Enabled = true);
