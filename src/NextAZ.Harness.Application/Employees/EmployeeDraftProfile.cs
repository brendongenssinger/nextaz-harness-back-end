using NextAZ.Harness.Domain.Agents;
namespace NextAZ.Harness.Application.Employees;
public sealed record EmployeeDraftProfile(Guid TenantId, string ProductId, Guid EmployeeId,
    AgentDefinition Agent, TenantAgentConfiguration Configuration);
