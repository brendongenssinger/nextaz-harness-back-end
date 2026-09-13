using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Contracts.Events;
namespace NextAZ.Harness.Application.Abstractions;
public interface IExecutionRepository
{
    Task<bool> TryCreateAsync(ExecutionSnapshot execution, CancellationToken cancellationToken);
    Task<ExecutionSnapshot?> TryAcquireLeaseAsync(Guid tenantId, Guid executionId, string leaseOwner,
        Guid leaseToken, DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task<bool> TryFinalizeAsync(ExecutionSnapshot execution, Guid leaseToken,
        HarnessExecutionEvent auditEvent, CancellationToken cancellationToken);
    Task<ExecutionSnapshot?> GetAsync(Guid tenantId, Guid executionId, CancellationToken cancellationToken);
    Task<ExecutionSnapshot?> GetByCorrelationAsync(Guid tenantId, string operation,
        string correlationId, CancellationToken cancellationToken);
}
