using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Status;

public sealed class BackfillProgressService(
    IBackfillStateStore store,
    IOptions<UniversalisOptions> universalis,
    IMemoryCache cache,
    TimeProvider time,
    ILogger<BackfillProgressService> logger)
{
    private const string CacheKey = "status_backfill_progress";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public Task<BackfillProgressResponse> GetAsync(CancellationToken ct)
    {
        // Shared build that never sees a caller's token, as in StatusMetricsService.
        var build = cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return new Lazy<Task<BackfillProgressResponse>>(BuildAsync);
        })!;
        return build.Value.WaitAsync(ct);
    }

    private async Task<BackfillProgressResponse> BuildAsync()
    {
        var now = time.GetUtcNow();
        var regions = universalis.Value.RegionsToUse;
        try
        {
            var buckets = await Task.WhenAll(
                regions.Select(region => store.GetBucketProgressAsync(region, BackfillLoops.Historical))).ConfigureAwait(false);

            return new BackfillProgressResponse(true, now.ToUnixTimeSeconds(), [.. regions.Zip(buckets, Summarize)]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Scylla unavailable for backfill progress");
            return new BackfillProgressResponse(false, now.ToUnixTimeSeconds(), []);
        }
    }

    // Min/Max over nullable values skip the nulls and return null when none are left.
    internal static RegionBackfillProgress Summarize(string region, IReadOnlyList<BackfillBucketProgress> buckets) =>
        new(
            region,
            buckets.Count,
            buckets.Count(b => b.CrawlComplete),
            ReachedBackTo: buckets.Min(b => b.EarliestImportAt)?.ToUnixTimeSeconds(),
            SlowestBucketAt: buckets.Where(b => !b.CrawlComplete).Max(b => b.EarliestImportAt)?.ToUnixTimeSeconds(),
            LastAdvancedAt: buckets.Max(b => b.PointerWrittenAt)?.ToUnixTimeSeconds());
}
