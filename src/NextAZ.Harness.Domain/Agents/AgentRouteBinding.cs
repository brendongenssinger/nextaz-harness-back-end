using System.Text.RegularExpressions;

namespace NextAZ.Harness.Domain.Agents;

public sealed record AgentRouteBinding
{
    private static readonly Regex RoutePartPattern = new(
        @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)*$",
        RegexOptions.CultureInvariant);

    public Guid TenantId { get; }
    public string Channel { get; }
    public string Operation { get; }
    public AgentId AgentId { get; }
    public AgentVersion AgentVersion { get; }
    public bool Enabled { get; }
    public int Revision { get; }
    public DateTimeOffset UpdatedAt { get; }
    public string UpdatedBy { get; }

    public AgentRouteBinding(Guid tenantId, string channel, string operation, AgentId agentId,
        AgentVersion agentVersion, bool enabled, int revision, DateTimeOffset updatedAt, string updatedBy)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ValidateRoutePart(channel, 32, nameof(channel));
        ValidateRoutePart(operation, 120, nameof(operation));
        if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
        if (updatedAt == default) throw new ArgumentException("UpdatedAt is required.", nameof(updatedAt));
        if (string.IsNullOrWhiteSpace(updatedBy) || updatedBy.Length > 200)
            throw new ArgumentException("UpdatedBy is required.", nameof(updatedBy));

        TenantId = tenantId;
        Channel = channel;
        Operation = operation;
        AgentId = agentId;
        AgentVersion = agentVersion;
        Enabled = enabled;
        Revision = revision;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
    }

    public static void ValidateRoutePart(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength || !RoutePartPattern.IsMatch(value))
            throw new ArgumentException("Use a stable lowercase route identifier.", parameterName);
    }
}
