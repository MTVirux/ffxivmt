using Prometheus;

namespace Ffmt.Core.Metrics;

/// <summary>
/// Single source of truth for every Prometheus instrument in the FFMT services.
/// Cardinality discipline: never label by item_id or sale_id (millions of values).
/// World IDs (~80), endpoint names (~20), op types (~10) are acceptable.
/// </summary>
public static class MetricsCatalog
{
    // HTTP RED (populated by HttpMetrics middleware)
    public static readonly Counter HttpRequestsTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_http_requests_total",
        "HTTP requests handled.",
        new CounterConfiguration { LabelNames = ["endpoint", "method", "status"] });

    public static readonly Histogram HttpRequestDurationSeconds = Prometheus.Metrics.CreateHistogram(
        "ffmt_http_request_duration_seconds",
        "HTTP request duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = ["endpoint", "method"],
            Buckets = Histogram.ExponentialBuckets(start: 0.001, factor: 2, count: 14),
        });

    // Scylla driver
    public static readonly Histogram ScyllaQueryDurationSeconds = Prometheus.Metrics.CreateHistogram(
        "ffmt_scylla_query_duration_seconds",
        "Client-observed CQL query latency.",
        new HistogramConfiguration
        {
            LabelNames = ["op"],
            Buckets = Histogram.ExponentialBuckets(start: 0.001, factor: 2, count: 14),
        });

    public static readonly Gauge ScyllaInflight = Prometheus.Metrics.CreateGauge(
        "ffmt_scylla_inflight",
        "In-flight CQL requests, by op.",
        new GaugeConfiguration { LabelNames = ["op"] });

    // Universalis WS ingest
    public static readonly Counter WsSalesReceivedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_ws_sales_received_total",
        "Sales received from the Universalis websocket.",
        new CounterConfiguration { LabelNames = ["world"] });

    public static readonly Counter WsInsertsTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_ws_inserts_total",
        "Sale-batch insert attempts originating from the WS consumer.",
        new CounterConfiguration { LabelNames = ["world", "result"] });

    public static readonly Gauge WsConnected = Prometheus.Metrics.CreateGauge(
        "ffmt_ws_connected",
        "1 when the websocket consumer is subscribed and the connection is open, else 0.",
        new GaugeConfiguration { LabelNames = ["world"] });

    // RankingCoalescer
    public static readonly Gauge CoalescerQueueDepth = Prometheus.Metrics.CreateGauge(
        "ffmt_coalescer_queue_depth",
        "Current depth of the ranking-refresh queue.");

    public static readonly Counter CoalescerDropsTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_coalescer_drops_total",
        "Refresh requests dropped because the bounded queue was full.");

    public static readonly Counter CoalescerCoalescedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_coalescer_coalesced_total",
        "Refresh requests dropped because the same (world,item) was refreshed inside the coalesce window.");

    public static readonly Gauge CoalescerWorkerBusy = Prometheus.Metrics.CreateGauge(
        "ffmt_coalescer_worker_busy",
        "1 when a coalescer worker is currently executing a refresh, else 0.",
        new GaugeConfiguration { LabelNames = ["worker_id"] });

    // Gilflux
    public static readonly Histogram GilfluxRefreshDurationSeconds = Prometheus.Metrics.CreateHistogram(
        "ffmt_gilflux_refresh_duration_seconds",
        "End-to-end duration of one RefreshAsync call (eight CQL queries).",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(start: 0.01, factor: 2, count: 12),
        });

    public static readonly Counter GilfluxRefreshErrorsTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_gilflux_refresh_errors_total",
        "RefreshAsync calls that threw (any exception).");

    public static readonly Counter DirtyPairsDrainedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_dirty_pairs_drained_total",
        "Dirty pairs drained from the deferred-sweep queue.");

    public static readonly Counter DecaySweepRowsScannedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_gilflux_decay_sweep_rows_scanned_total",
        "gilflux_rankings rows examined by the decay sweep.");

    public static readonly Counter DecaySweepRowsDeletedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_gilflux_decay_sweep_rows_deleted_total",
        "gilflux_rankings rows deleted by the decay sweep because every timeframe had lapsed.");

    public static readonly Counter DecaySweepRowsEnqueuedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_gilflux_decay_sweep_rows_enqueued_total",
        "Stale gilflux_rankings rows the decay sweep re-queued for a recompute.");

    // Backfill
    public static readonly Counter BackfillPagesTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_backfill_pages_total",
        "Universalis history pages fetched by the backfill service.",
        new CounterConfiguration { LabelNames = ["region", "result"] });

    public static readonly Counter BackfillRowsTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_backfill_rows_total",
        "Sale rows inserted by the backfill service.",
        new CounterConfiguration { LabelNames = ["region"] });

    public static readonly Counter BackfillPointerStalledTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_backfill_pointer_stalled_total",
        "Backfill passes that finished with failed chunks and so left their pointer unmoved.",
        new CounterConfiguration { LabelNames = ["region", "loop"] });

    public static readonly Gauge BackfillStalledBuckets = Prometheus.Metrics.CreateGauge(
        "ffmt_backfill_stalled_buckets",
        "Catalogue buckets whose request failed this pass, so their pointer did not advance.",
        new GaugeConfiguration { LabelNames = ["region", "loop"] });

    public static readonly Gauge BackfillHistoryOldestSeconds = Prometheus.Metrics.CreateGauge(
        "ffmt_backfill_history_oldest_seconds",
        "Age of the oldest imported sale day, measured from the least advanced bucket.",
        new GaugeConfiguration { LabelNames = ["region"] });

    public static readonly Gauge BackfillState = Prometheus.Metrics.CreateGauge(
        "ffmt_backfill_state",
        "Backfill loop state per region and loop: 0=idle, 1=running, 2=paused, 3=error.",
        new GaugeConfiguration { LabelNames = ["region", "loop"] });

    // Sale anomaly quarantine
    public static readonly Counter SalesQuarantinedTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_sales_quarantined_total",
        "Sales diverted to sales_quarantine as price anomalies.",
        new CounterConfiguration { LabelNames = ["world", "reason"] });

    public static readonly Counter SalesNoBaselineTotal = Prometheus.Metrics.CreateCounter(
        "ffmt_sales_no_baseline_total",
        "Sales accepted unevaluated because no trusted price baseline existed for the slice.",
        new CounterConfiguration { LabelNames = ["world"] });

    public static readonly Gauge PriceBaselineRows = Prometheus.Metrics.CreateGauge(
        "ffmt_price_baseline_rows",
        "Rows in the in-process price-baseline snapshot.");

    public static readonly Gauge PriceBaselineAgeSeconds = Prometheus.Metrics.CreateGauge(
        "ffmt_price_baseline_age_seconds",
        "Age of the in-process price-baseline snapshot.");

    public static readonly Histogram BaselineJobDurationSeconds = Prometheus.Metrics.CreateHistogram(
        "ffmt_baseline_job_duration_seconds",
        "Duration of one update-baselines run.",
        new HistogramConfiguration { Buckets = Histogram.ExponentialBuckets(1, 2, 14) });

    /// <summary>Used by tests to enumerate the catalog. Order matches definition order above.</summary>
    public static IReadOnlyList<Collector> All =>
    [
        HttpRequestsTotal,
        HttpRequestDurationSeconds,
        ScyllaQueryDurationSeconds,
        ScyllaInflight,
        WsSalesReceivedTotal,
        WsInsertsTotal,
        WsConnected,
        CoalescerQueueDepth,
        CoalescerDropsTotal,
        CoalescerCoalescedTotal,
        CoalescerWorkerBusy,
        GilfluxRefreshDurationSeconds,
        GilfluxRefreshErrorsTotal,
        DirtyPairsDrainedTotal,
        DecaySweepRowsScannedTotal,
        DecaySweepRowsDeletedTotal,
        DecaySweepRowsEnqueuedTotal,
        BackfillPagesTotal,
        BackfillRowsTotal,
        BackfillPointerStalledTotal,
        BackfillStalledBuckets,
        BackfillHistoryOldestSeconds,
        BackfillState,
        SalesQuarantinedTotal,
        SalesNoBaselineTotal,
        PriceBaselineRows,
        PriceBaselineAgeSeconds,
        BaselineJobDurationSeconds,
    ];
}
