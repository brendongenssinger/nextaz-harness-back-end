using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.Application.Administration;

public interface IHarnessUsageReader
{
    Task<HarnessUsageReport> ReadAsync(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string costCurrency,
        CancellationToken cancellationToken);
}
