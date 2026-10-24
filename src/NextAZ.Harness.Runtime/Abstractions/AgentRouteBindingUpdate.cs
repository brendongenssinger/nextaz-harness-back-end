namespace NextAZ.Harness.Runtime.Abstractions;

public sealed record AgentRouteBindingUpdate(
    Guid TenantId,
    string Channel,
    string Operation,
    AgentId AgentId,
    AgentVersion AgentVersion,
    bool Enabled,
    int? ExpectedRevision,
    string UpdatedBy);
