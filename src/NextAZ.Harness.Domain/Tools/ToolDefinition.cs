namespace NextAZ.Harness.Domain.Tools;
public sealed record ToolDefinition(ToolId ToolId, string Name, string Description,
    string InputSchema, ToolRiskLevel RiskLevel, bool RequiresTenantContext,
    TimeSpan Timeout, string RequiredPermission, int Version = 1);
