using Ffmt.Core.External;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.Status;

public sealed class StatusMetricsService(
    IPrometheusClient prometheus,
    IMemoryCache cache,
    TimeProvider time,
    ILogger<StatusMetricsService> logger)
{
    private const string CacheKey = "status_metrics";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

    // The only PromQL the public endpoint ever runs.
    internal static readonly IReadOnlyDictionary<string, string> Queries = new Dictionary<string, string>
    {
        ["requests_per_second"] = "sum(rate(ffmt_http_requests_total[5m]))",
        ["error_rate"] = "(sum(rate(ffmt_http_requests_total{status=~\"5..\"}[5m])) or vector(0)) / sum(rate(ffmt_http_requests_total[5m]))",
        ["sales_per_second"] = "sum(rate(ffmt_ws_sales_received_total[5m]))",
        ["worlds_connected"] = "sum(ffmt_ws_connected)",
        ["backfill_rows_per_second"] = "sum(rate(ffmt_backfill_rows_total[5m]))",
    };

    internal const string WorldsTotalQuery = "count(ffmt_ws_connected)";
    internal const string StoredBatchesLast10mQuery = "sum(increase(ffmt_ws_inserts_total{result=\"ok\"}[10m]))";

    public Task<StatusMetricsResponse> GetAsync(CancellationToken ct)
    {
        // One build is shared by every caller and never sees a caller's token, so clients that
        // disconnect early cannot cancel it and trigger a fresh round of Prometheus queries.
        var build = cache.GetOrCreate(CacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return new Lazy<Task<StatusMetricsResponse>>(BuildAsync);
        })!;
        return build.Value.WaitAsync(ct);
    }

    private async Task<StatusMetricsResponse> BuildAsync()
    {
        var now = time.GetUtcNow();
        try
        {
            var snapshots = Queries.ToDictionary(q => q.Key, q => LoadAsync(q.Value, now));
            var worldsTotal = prometheus.QueryAsync(WorldsTotalQuery);
            var storedBatchesLast10m = prometheus.QueryAsync(StoredBatchesLast10mQuery);
            await Task.WhenAll([.. snapshots.Values, worldsTotal, storedBatchesLast10m]).ConfigureAwait(false);

            var metrics = snapshots.ToDictionary(s => s.Key, s => s.Value.Result);
            metrics["worlds_connected"] = metrics["worlds_connected"] with { Total = worldsTotal.Result };

            var verdict = StatusRules.Evaluate(
                storedBatchesLast10m.Result,
                metrics["error_rate"].Value,
                metrics["worlds_connected"].Value,
                worldsTotal.Result);

            return new StatusMetricsResponse(true, verdict.State, verdict.Reasons, now.ToUnixTimeSeconds(), metrics);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Prometheus unavailable for status metrics");
            return new StatusMetricsResponse(false, "unknown", [], now.ToUnixTimeSeconds(), new Dictionary<string, MetricSnapshot>());
        }
    }

    private async Task<MetricSnapshot> LoadAsync(string promql, DateTimeOffset now)
    {
        var value = prometheus.QueryAsync(promql);
        var series = prometheus.QueryRangeAsync(promql, now - Window, now, Step);
        await Task.WhenAll(value, series).ConfigureAwait(false);
        return new MetricSnapshot(value.Result, series.Result);
    }
}
