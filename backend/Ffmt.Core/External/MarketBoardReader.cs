using System.Runtime.ExceptionServices;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Logging;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.External;

/// <summary>
/// Universalis' own gateway times out on datacenter- and region-wide multi-item queries, so those
/// are fanned out one world at a time and aggregated here. The merge mirrors what Universalis does
/// server side: cheapest listing wins, velocities add up, stack histograms merge bucketwise.
/// </summary>
public sealed class MarketBoardReader(
    IUniversalisClient universalis,
    WorldStructureService structure,
    LocationResolver locations,
    ILogger<MarketBoardReader> logger)
{
    // Universalis answers 504 above this many ids even for a single world.
    private const int MaxItemsPerRequest = 50;

    // Process-wide, so concurrent callers cannot together stampede Universalis.
    private const int MaxConcurrentRequests = 20;
    private static readonly SemaphoreSlim Gate = new(MaxConcurrentRequests, MaxConcurrentRequests);

    public async Task<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> GetAsync(
        string location, IReadOnlyList<int> itemIds, CancellationToken ct = default)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<int, UniversalisMarketBoardListing>();
        }

        using var _ = LogChannelScope.Begin(logger, LogChannels.UniversalisApi);

        var worlds = await ResolveTargetsAsync(location, ct).ConfigureAwait(false);
        var chunks = itemIds.Chunk(MaxItemsPerRequest).ToList();

        var results = await Task.WhenAll(
            from world in worlds
            from chunk in chunks
            select FetchAsync(world, chunk, ct)).ConfigureAwait(false);

        var listings = results.Where(r => r.Listings is not null).Select(r => r.Listings!).ToList();
        if (listings.Count == 0)
        {
            ExceptionDispatchInfo.Throw(results[0].Error!);
        }

        if (listings.Count != results.Length)
        {
            logger.LogWarning(
                "Universalis fan-out for {Location}: {Failed}/{Total} requests failed, aggregate is partial.",
                location, results.Length - listings.Count, results.Length);
        }

        return Merge(listings);
    }

    /// <summary>A location we cannot resolve is passed through untouched so Universalis rejects it.</summary>
    private async Task<IReadOnlyList<string>> ResolveTargetsAsync(string location, CancellationToken ct)
    {
        var resolution = await locations.ResolveAsync(location, ct).ConfigureAwait(false);
        if (resolution is null)
        {
            return [location];
        }

        var worlds = await structure.GetWorldsAsync(ct).ConfigureAwait(false);
        var matched = worlds.Where(resolution.Matches).Select(w => w.Name).ToList();
        return matched.Count > 0 ? matched : [location];
    }

    private async Task<(IReadOnlyDictionary<int, UniversalisMarketBoardListing>? Listings, Exception? Error)> FetchAsync(
        string world, int[] itemIds, CancellationToken ct)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var listings = await universalis.GetMarketBoardDataAsync(world, itemIds, ct).ConfigureAwait(false);
            return (listings, null);
        }
        // One dead world degrades the aggregate; only the caller hanging up aborts the whole read.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Universalis market board request failed for {World} ({Count} ids).", world, itemIds.Length);
            return (null, ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static IReadOnlyDictionary<int, UniversalisMarketBoardListing> Merge(
        IEnumerable<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> responses)
    {
        var minPrice = new Dictionary<int, int>();
        var velocity = new Dictionary<int, double>();
        var histogram = new Dictionary<int, Dictionary<int, int>>();

        foreach (var response in responses)
        {
            foreach (var (id, listing) in response)
            {
                // A world with nothing listed reports 0, which must not win the cheapest-listing race.
                if (listing.MinPrice > 0)
                {
                    minPrice[id] = minPrice.TryGetValue(id, out var cheapest)
                        ? Math.Min(cheapest, listing.MinPrice)
                        : listing.MinPrice;
                }

                velocity[id] = velocity.GetValueOrDefault(id) + listing.RegularSaleVelocity;

                if (!histogram.TryGetValue(id, out var buckets))
                {
                    histogram[id] = buckets = [];
                }
                foreach (var (size, occurrences) in listing.StackSizeHistogram)
                {
                    buckets[size] = buckets.GetValueOrDefault(size) + occurrences;
                }
            }
        }

        var merged = new Dictionary<int, UniversalisMarketBoardListing>(velocity.Count);
        foreach (var (id, sales) in velocity)
        {
            merged[id] = new UniversalisMarketBoardListing(minPrice.GetValueOrDefault(id), sales, histogram[id]);
        }
        return merged;
    }
}
