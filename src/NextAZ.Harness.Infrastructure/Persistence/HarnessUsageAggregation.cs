using NextAZ.Harness.Contracts.Administration;

namespace NextAZ.Harness.Infrastructure.Persistence;

internal static class HarnessUsageAggregation
{
    public static HarnessUsageReport Build(
        Guid tenantId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string costCurrency,
        IEnumerable<HarnessUsageRow> rows)
    {
        var items = rows.ToArray();
        var groups = items
            .GroupBy(row => new
            {
                row.AgentId,
                row.AgentVersion,
                row.Operation,
                Provider = row.ProviderId ?? "not-recorded",
                Model = row.ModelId ?? "not-recorded"
            })
            .Select(group => new HarnessUsageGroup(
                group.Key.AgentId,
                group.Key.AgentVersion,
                group.Key.Operation,
                group.Key.Provider,
                group.Key.Model,
                group.Count(),
                group.Count(row => row.Status == "Completed"),
                group.Count(row => row.Status is "Failed" or "TimedOut" or "Cancelled"),
                group.Sum(row => (long)row.InputTokens),
                group.Sum(row => (long)row.OutputTokens),
                group.Where(row => row.EstimatedCost.HasValue)
                    .Sum(row => row.EstimatedCost ?? 0m),
                group.Count(row => row.EstimatedCost.HasValue),
                group.Count(row => !row.EstimatedCost.HasValue)))
            .OrderByDescending(group => group.KnownEstimatedCost)
            .ThenBy(group => group.AgentId, StringComparer.Ordinal)
            .ThenBy(group => group.Operation, StringComparer.Ordinal)
            .ToArray();

        return new HarnessUsageReport(
            tenantId,
            fromUtc,
            toUtc,
            items.Length,
            items.Count(row => row.Status == "Completed"),
            items.Count(row => row.Status is "Failed" or "TimedOut" or "Cancelled"),
            items.Sum(row => (long)row.InputTokens),
            items.Sum(row => (long)row.OutputTokens),
            items.Where(row => row.EstimatedCost.HasValue).Sum(row => row.EstimatedCost ?? 0m),
            items.Count(row => row.EstimatedCost.HasValue),
            items.Count(row => !row.EstimatedCost.HasValue),
            costCurrency,
            groups);
    }
}

internal sealed record HarnessUsageRow(
    string AgentId,
    int AgentVersion,
    string Operation,
    string Status,
    string? ProviderId,
    string? ModelId,
    int InputTokens,
    int OutputTokens,
    decimal? EstimatedCost);
