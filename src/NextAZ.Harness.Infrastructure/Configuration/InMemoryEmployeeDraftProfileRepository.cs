using System.Collections.Frozen;
using NextAZ.Harness.Application.Employees;
namespace NextAZ.Harness.Infrastructure.Configuration;
public sealed class InMemoryEmployeeDraftProfileRepository(IEnumerable<EmployeeDraftProfile> profiles) : IEmployeeDraftProfileRepository
{
    private readonly IReadOnlyDictionary<(Guid, string, Guid), EmployeeDraftProfile> _profiles = profiles
        .Select(p => p with {
            Agent = p.Agent with { AllowedTools = p.Agent.AllowedTools.ToFrozenSet(), RequiredPolicies = p.Agent.RequiredPolicies.ToFrozenSet() },
            Configuration = p.Configuration with { AllowedTools = p.Configuration.AllowedTools.ToFrozenSet() }
        }).ToFrozenDictionary(p => (p.TenantId, p.ProductId, p.EmployeeId));
    public Task<EmployeeDraftProfile?> GetAsync(Guid tenantId, string productId, Guid employeeId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant required.", nameof(tenantId));
        return Task.FromResult(_profiles.GetValueOrDefault((tenantId, productId, employeeId)));
    }
}
