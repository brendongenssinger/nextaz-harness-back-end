namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class AgentModelRouteRecord
{
    public Guid TenantId { get; set; }
    public string AgentId { get; set; } = string.Empty;
    public int AgentVersion { get; set; }
    public int Priority { get; set; }
    public Guid ProviderConnectionId { get; set; }
    public string ModelPolicy { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ProviderConnectionRecord ProviderConnection { get; set; } = null!;
}
