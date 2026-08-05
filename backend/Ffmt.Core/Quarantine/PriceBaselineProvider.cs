using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Quarantine;

public sealed class PriceBaselineProvider(
    IScyllaSession scylla,
    IOptions<UniversalisOptions> universalis,
    IOptions<QuarantineOptions> options,
    ILogger<PriceBaselineProvider> logger) : IPriceBaselineProvider
{
    private const string CqlSelectRegion = """
        SELECT item_id, hq, median_unit_price, sample_count, computed_at
        FROM item_price_baseline
        WHERE region = ?
        """;

    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private FrozenDictionary<(int ItemId, string Region, bool Hq), PriceBaseline> _snapshot =
        FrozenDictionary<(int ItemId, string Region, bool Hq), PriceBaseline>.Empty;

    private DateTimeOffset _loadedAt = DateTimeOffset.MinValue;
    private int _reloading;

    public async Task EnsureLoadedAsync(CancellationToken ct = default)
    {
        if (_loadedAt == DateTimeOffset.MinValue)
        {
            await ReloadAsync(ct).ConfigureAwait(false);
            return;
        }

        MetricsCatalog.PriceBaselineAgeSeconds.Set((DateTimeOffset.UtcNow - _loadedAt).TotalSeconds);

        var refreshAfter = TimeSpan.FromMinutes(Math.Max(1, options.Value.BaselineRefreshMinutes));
        if (DateTimeOffset.UtcNow - _loadedAt < refreshAfter)
        {
            return;
        }

        // Stale: refresh in the background and serve the current snapshot to this caller.
        if (Interlocked.CompareExchange(ref _reloading, 1, 0) == 0)
        {
            _ = Task.Run(async () =>
            {
                try { await ReloadAsync(CancellationToken.None).ConfigureAwait(false); }
                finally { Interlocked.Exchange(ref _reloading, 0); }
            }, CancellationToken.None);
        }
    }

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await _loadLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var built = new Dictionary<(int ItemId, string Region, bool Hq), PriceBaseline>();
            var stmt = await scylla.PrepareAsync(CqlSelectRegion, ct).ConfigureAwait(false);

            foreach (var region in universalis.Value.RegionsToUse)
            {
                var rows = await scylla.MeasuredExecuteAsync(stmt.Bind(region), "baseline_load").ConfigureAwait(false);
                foreach (var row in rows)
                {
                    built[(row.GetValue<int>("item_id"), region, row.GetValue<bool>("hq"))] = new PriceBaseline(
                        row.GetValue<long>("median_unit_price"),
                        row.GetValue<int>("sample_count"),
                        row.GetValue<DateTimeOffset>("computed_at"));
                }
            }

            _snapshot = built.ToFrozenDictionary();
            _loadedAt = DateTimeOffset.UtcNow;

            MetricsCatalog.PriceBaselineRows.Set(_snapshot.Count);
            MetricsCatalog.PriceBaselineAgeSeconds.Set(0);
            logger.LogInformation("Loaded {Count} price baselines across {Regions} region(s).",
                _snapshot.Count, universalis.Value.RegionsToUse.Length);
        }
        catch (Exception ex)
        {
            // Fail open. An unreadable baseline table must never break ingest; the previous
            // snapshot stays in place and the age gauge climbs so it is visible in Grafana.
            logger.LogWarning(ex, "Price-baseline load failed; retaining the previous snapshot of {Count} row(s).",
                _snapshot.Count);
            if (_loadedAt == DateTimeOffset.MinValue)
            {
                _loadedAt = DateTimeOffset.UtcNow;
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public bool TryGet(int itemId, string region, bool hq, [MaybeNullWhen(false)] out PriceBaseline baseline) =>
        _snapshot.TryGetValue((itemId, region, hq), out baseline);

    internal void SetSnapshotForTest(IDictionary<(int ItemId, string Region, bool Hq), PriceBaseline> rows)
    {
        _snapshot = rows.ToFrozenDictionary();
        _loadedAt = DateTimeOffset.UtcNow;
    }
}
