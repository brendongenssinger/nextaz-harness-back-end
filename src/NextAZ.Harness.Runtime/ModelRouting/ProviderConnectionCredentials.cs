namespace NextAZ.Harness.Runtime.ModelRouting;

public sealed record ProviderConnectionCredentials(Guid ProviderConnectionId, string ProviderId,
    Uri Endpoint, string ApiKey);
