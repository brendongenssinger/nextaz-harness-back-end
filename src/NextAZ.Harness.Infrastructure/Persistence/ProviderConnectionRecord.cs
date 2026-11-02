namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class ProviderConnectionRecord
{
    public Guid TenantId { get; set; }
    public Guid ProviderConnectionId { get; set; }
    public string ConnectionKey { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public byte[] EncryptedApiKey { get; set; } = [];
    public byte[] ApiKeyNonce { get; set; } = [];
    public byte[] ApiKeyTag { get; set; } = [];
    public byte[] EncryptedDataKey { get; set; } = [];
    public string KeyEncryptionKeyId { get; set; } = string.Empty;
    public string EncryptionAlgorithm { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<AgentModelRouteRecord> AgentModelRoutes { get; set; } = [];
}
