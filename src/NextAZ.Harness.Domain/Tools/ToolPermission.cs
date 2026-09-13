using NextAZ.Harness.Domain.Agents;
namespace NextAZ.Harness.Domain.Tools;
public sealed record ToolPermission(Guid TenantId, AgentId AgentId, ToolId ToolId);
