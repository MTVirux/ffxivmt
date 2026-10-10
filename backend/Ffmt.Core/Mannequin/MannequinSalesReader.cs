using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Mannequin;

public sealed record MannequinFeedQuery(string? TargetLocation, DateTimeOffset? Before, int Limit, bool? Hq, int MinUnitPrice);

public sealed record MannequinFeedPage(IReadOnlyList<Sale> Sales, DateTimeOffset? NextBefore);

public sealed class MannequinSalesReader(
    IMannequinSaleStore store,
    WorldStructureService worldStructure,
    LocationResolver resolver,
    IMemoryCache cache,
    IOptions<MannequinOptions> options)
{
    public async Task<MannequinFeedPage?> GetAsync(MannequinFeedQuery query, CancellationToken ct = default)
    {
        LocationResolution? resolution = null;
        if (query.TargetLocation is not null)
        {
            resolution = await resolver.ResolveAsync(query.TargetLocation, ct).ConfigureAwait(false);
            if (resolution is null)
            {
                return null;
            }
        }

        var cacheKey = query.Before is null
            ? $"mannequin:{resolution?.CanonicalName ?? "*"}:{query.Limit}:{query.Hq?.ToString() ?? "*"}:{query.MinUnitPrice}"
            : null;
        if (cacheKey is not null && cache.TryGetValue(cacheKey, out MannequinFeedPage? cached) && cached is not null)
        {
            return cached;
        }

        var worlds = await worldStructure.GetWorldsAsync(ct).ConfigureAwait(false);
        var worldIds = worlds.Where(w => resolution is null || resolution.Matches(w)).Select(w => w.Id).ToHashSet();
        var days = await store.GetDaysAsync(ct).ConfigureAwait(false);

        var page = worldIds.Count == 0 || days.Count == 0
            ? new MannequinFeedPage([], null)
            : await WalkAsync(query, worldIds, days, ct).ConfigureAwait(false);

        if (cacheKey is not null)
        {
            cache.Set(cacheKey, page, TimeSpan.FromSeconds(Math.Max(1, options.Value.FeedCacheSeconds)));
        }

        return page;
    }

    private async Task<MannequinFeedPage> WalkAsync(
        MannequinFeedQuery query, HashSet<int> worldIds, IReadOnlyList<DateOnly> days, CancellationToken ct)
    {
        // A minute of slack on the first page: Universalis timestamps can run slightly ahead of our clock.
        var before = query.Before ?? DateTimeOffset.UtcNow.AddMinutes(1);
        var newestDay = MannequinCql.DayOf(before.AddTicks(-1));
        var candidates = days.Where(d => d <= newestDay).OrderDescending().ToList();
        var maxDays = Math.Max(1, options.Value.MaxDaysPerRequest);

        var collected = new List<Sale>();
        var scanned = 0;
        foreach (var day in candidates)
        {
            if (scanned == maxDays || collected.Count >= query.Limit)
            {
                break;
            }

            var rows = await store.GetByDayAsync(day, before, ct).ConfigureAwait(false);
            collected.AddRange(rows.Where(s =>
                worldIds.Contains(s.WorldId) &&
                (query.Hq is null || s.Hq == query.Hq) &&
                s.UnitPrice >= query.MinUnitPrice));
            scanned++;
        }

        var ordered = collected
            .OrderByDescending(s => s.SaleTime)
            .ThenBy(s => s.ItemId)
            .ThenBy(s => s.BuyerName, StringComparer.Ordinal)
            .ToList();
        var page = TakeWithTies(ordered, query.Limit);

        DateTimeOffset? next = null;
        if (page.Count < ordered.Count)
        {
            next = page[^1].SaleTime;
        }
        else if (scanned < candidates.Count)
        {
            next = new DateTimeOffset(candidates[scanned - 1].ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        return new MannequinFeedPage(page, next);
    }

    // The cursor is exclusive, so a page must never end partway through a run of equal timestamps.
    private static List<Sale> TakeWithTies(List<Sale> ordered, int limit)
    {
        if (ordered.Count <= limit)
        {
            return ordered;
        }

        var end = limit;
        while (end < ordered.Count && ordered[end].SaleTime == ordered[limit - 1].SaleTime)
        {
            end++;
        }

        return ordered.GetRange(0, end);
    }
}
