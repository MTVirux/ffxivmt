using Ffmt.Core.Models;

namespace Ffmt.Core.Gilflux;

public sealed record RankingSweepPlan(
    IReadOnlyList<(int WorldId, int ItemId)> Delete,
    IReadOnlyList<(int WorldId, int ItemId)> Refresh);

public static class RankingDecay
{
    /// <summary>
    /// A stored timeframe sum covers [updated_at - T, updated_at]. Two independent facts force it
    /// to zero, and a timeframe is only live when neither applies:
    /// no sale can land after updated_at without triggering a refresh, so a row older than T has
    /// an empty live window; and last_sale_time is the most recent sale there is, so a sale older
    /// than T likewise leaves nothing inside it. The second is not implied by the first - a row
    /// refreshed yesterday can still carry a sale that sat at the far edge of its window.
    /// Timeframes that survive both may be overstated and need a real refresh to correct.
    /// </summary>
    public static IReadOnlyDictionary<string, long> Apply(
        IReadOnlyDictionary<string, long> rankings,
        IReadOnlyDictionary<string, long> timeframesMs,
        long? updatedAtMs,
        long? lastSaleTimeMs,
        DateTimeOffset now)
    {
        var nowMs = now.ToUnixTimeMilliseconds();
        var rowAgeMs = updatedAtMs is null ? long.MaxValue : nowMs - updatedAtMs.Value;
        var saleAgeMs = lastSaleTimeMs is null or 0L ? long.MaxValue : nowMs - lastSaleTimeMs.Value;
        var staleMs = Math.Max(rowAgeMs, saleAgeMs);

        var result = new Dictionary<string, long>(rankings.Count);
        foreach (var (key, value) in rankings)
        {
            var live = timeframesMs.TryGetValue(key, out var durationMs) && staleMs < durationMs;
            result[key] = live ? value : 0L;
        }

        return result;
    }

    public static bool IsExhausted(IReadOnlyDictionary<string, long> rankings)
    {
        foreach (var value in rankings.Values)
        {
            if (value != 0L)
            {
                return false;
            }
        }

        return true;
    }

    public static RankingSweepPlan Plan(
        IEnumerable<GilfluxRanking> rows,
        IReadOnlyDictionary<string, long> timeframesMs,
        TimeSpan staleAfter,
        int maxRefresh,
        DateTimeOffset now,
        int maxDelete = int.MaxValue)
    {
        var nowMs = now.ToUnixTimeMilliseconds();
        var staleAfterMs = (long)staleAfter.TotalMilliseconds;

        var exhausted = new List<(long UpdatedAt, int WorldId, int ItemId)>();
        var stale = new List<(long UpdatedAt, int WorldId, int ItemId)>();

        foreach (var row in rows)
        {
            if (row.WorldId is not int worldId)
            {
                continue;
            }

            var updatedAt = row.UpdatedAt ?? 0L;

            if (IsExhausted(Apply(row.Rankings, timeframesMs, row.UpdatedAt, row.LastSaleTime, now)))
            {
                exhausted.Add((updatedAt, worldId, row.ItemId));
            }
            else if (nowMs - updatedAt >= staleAfterMs)
            {
                stale.Add((updatedAt, worldId, row.ItemId));
            }
        }

        return new RankingSweepPlan(
            Delete: Oldest(exhausted, maxDelete),
            Refresh: Oldest(stale, maxRefresh));
    }

    private static List<(int WorldId, int ItemId)> Oldest(
        List<(long UpdatedAt, int WorldId, int ItemId)> candidates, int take) =>
        candidates
            .OrderBy(c => c.UpdatedAt)
            .Take(Math.Max(0, take))
            .Select(c => (c.WorldId, c.ItemId))
            .ToList();
}
