namespace NextAZ.Harness.Application.Employees;
public interface IEmployeeDraftProfileRepository
{
    Task<EmployeeDraftProfile?> GetAsync(Guid tenantId, string productId, Guid employeeId, CancellationToken cancellationToken);
}
