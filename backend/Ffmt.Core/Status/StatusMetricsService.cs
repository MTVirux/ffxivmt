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
    internal const string SalesLast10mQuery = "sum(increase(ffmt_ws_sales_received_total[10m]))";

    public async Task<StatusMetricsResponse> GetAsync(CancellationToken ct)
    {
        if (cache.TryGetValue<StatusMetricsResponse>(CacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var response = await BuildAsync(ct).ConfigureAwait(false);
        cache.Set(CacheKey, response, CacheTtl);
        return response;
    }

    private async Task<StatusMetricsResponse> BuildAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        try
        {
            var snapshots = Queries.ToDictionary(q => q.Key, q => LoadAsync(q.Value, now, ct));
            var worldsTotal = prometheus.QueryAsync(WorldsTotalQuery, ct);
            var salesLast10m = prometheus.QueryAsync(SalesLast10mQuery, ct);
            await Task.WhenAll([.. snapshots.Values, worldsTotal, salesLast10m]).ConfigureAwait(false);

            var metrics = snapshots.ToDictionary(s => s.Key, s => s.Value.Result);
            metrics["worlds_connected"] = metrics["worlds_connected"] with { Total = worldsTotal.Result };

            var verdict = StatusRules.Evaluate(
                salesLast10m.Result,
                metrics["error_rate"].Value,
                metrics["worlds_connected"].Value,
                worldsTotal.Result);

            return new StatusMetricsResponse(true, verdict.State, verdict.Reasons, now.ToUnixTimeSeconds(), metrics);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Prometheus unavailable for status metrics");
            return new StatusMetricsResponse(false, "unknown", [], now.ToUnixTimeSeconds(), new Dictionary<string, MetricSnapshot>());
        }
    }

    private async Task<MetricSnapshot> LoadAsync(string promql, DateTimeOffset now, CancellationToken ct)
    {
        var value = prometheus.QueryAsync(promql, ct);
        var series = prometheus.QueryRangeAsync(promql, now - Window, now, Step, ct);
        await Task.WhenAll(value, series).ConfigureAwait(false);
        return new MetricSnapshot(value.Result, series.Result);
    }
}
