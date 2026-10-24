namespace NextAZ.Harness.Infrastructure.Persistence;

internal sealed class AgentRouteBindingRecord
{
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public int AgentVersion { get; set; }
    public bool Enabled { get; set; }
    public int Revision { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}
