using Microsoft.EntityFrameworkCore;
using NextAZ.Harness.Application.Administration;
using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.Infrastructure.Persistence;

public sealed class PostgresHarnessUsageReader(
    IDbContextFactory<HarnessDbContext> contextFactory) : IHarnessUsageReader
{
    public async Task<HarnessUsageReport> ReadAsync(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string costCurrency,
        CancellationToken cancellationToken)
    {
        Validate(tenantId, fromUtc, toUtc, costCurrency);
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await context.Executions
            .AsNoTracking()
            .Where(row => row.TenantId == tenantId &&
                row.StartedAt >= fromUtc && row.StartedAt < toUtc)
            .Select(row => new HarnessUsageRow(
                row.AgentId,
                row.AgentVersion,
                row.Operation,
                row.Status,
                row.ProviderId,
                row.ModelId,
                row.InputTokens,
                row.OutputTokens,
                row.EstimatedCost))
            .ToArrayAsync(cancellationToken);

        return HarnessUsageAggregation.Build(tenantId, fromUtc, toUtc, costCurrency, rows);
    }

    internal static void Validate(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string costCurrency)
    {
        if (tenantId == Guid.Empty || fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(93) ||
            string.IsNullOrWhiteSpace(costCurrency) || costCurrency.Length != 3)
            throw new ArgumentException("A valid tenant, period of up to 93 days and currency are required.");
    }
}
