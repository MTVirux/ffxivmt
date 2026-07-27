using Ffmt.Core.Models;

namespace Ffmt.Core.Gilflux;

public sealed record RankingSweepPlan(
    IReadOnlyList<(int WorldId, int ItemId)> Delete,
    IReadOnlyList<(int WorldId, int ItemId)> Refresh);

public static class RankingDecay
{
    /// <summary>
    /// A stored timeframe sum covers [updated_at - T, updated_at]. No sale can land after
    /// updated_at without triggering a refresh, so once the row is older than T the live window
    /// [now - T, now] contains nothing and the sum is provably zero. Timeframes the row is still
    /// younger than may be overstated and need a real refresh to correct.
    /// </summary>
    public static IReadOnlyDictionary<string, long> Apply(
        IReadOnlyDictionary<string, long> rankings,
        IReadOnlyDictionary<string, long> timeframesMs,
        long? updatedAtMs,
        DateTimeOffset now)
    {
        var ageMs = updatedAtMs is null ? long.MaxValue : now.ToUnixTimeMilliseconds() - updatedAtMs.Value;
        var result = new Dictionary<string, long>(rankings.Count);

        foreach (var (key, value) in rankings)
        {
            var live = timeframesMs.TryGetValue(key, out var durationMs) && ageMs < durationMs;
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

            if (IsExhausted(Apply(row.Rankings, timeframesMs, row.UpdatedAt, now)))
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
