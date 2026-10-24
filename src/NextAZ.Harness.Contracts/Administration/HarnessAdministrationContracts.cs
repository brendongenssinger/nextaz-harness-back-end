namespace NextAZ.Harness.Contracts.Administration;

public sealed record HarnessAdministrationOverview(
    Guid TenantId,
    string Mode,
    string Persistence,
    string Provider,
    string Messaging,
    IReadOnlyList<HarnessAdministrationAgent> Agents,
    IReadOnlyList<HarnessAdministrationRouteBinding> Routes);

public sealed record HarnessAdministrationAgent(
    string AgentId,
    int AgentVersion,
    string Name,
    string Vertical,
    string Objective,
    bool AgentEnabled,
    IReadOnlyList<string> RequiredPolicies,
    IReadOnlyList<string> DefinedTools,
    bool TenantConfigured,
    bool TenantEnabled,
    int? ConfigurationVersion,
    string? Instructions,
    string? ModelPolicy,
    string? MemoryPolicy,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyList<HarnessAdministrationModelRoute> ModelRoutes,
    HarnessAdministrationExecutionLimits? Limits);

public sealed record HarnessAdministrationModelRoute(string Provider, string Model, int TimeoutSeconds);

public sealed record HarnessAdministrationExecutionLimits(
    int MaxSteps,
    int MaxToolCalls,
    int MaxOutputTokens,
    int TimeoutSeconds);

public sealed record HarnessAdministrationRouteBinding(
    string Channel,
    string Operation,
    string AgentId,
    int AgentVersion,
    bool Enabled,
    int Revision,
    DateTimeOffset UpdatedAt,
    string UpdatedBy);

public sealed record SaveHarnessAdministrationRouteBinding(
    string Channel,
    string Operation,
    string AgentId,
    int AgentVersion,
    bool Enabled,
    int? ExpectedRevision);
