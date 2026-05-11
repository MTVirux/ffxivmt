using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Core.Metrics;

public static class ScyllaInstrumentation
{
    /// <summary>
    /// Executes a CQL statement with prometheus-net duration + inflight instrumentation.
    /// The <paramref name="op"/> label must be a low-cardinality constant string
    /// (e.g. "sale_insert", "gilflux_refresh", "backfill_state_read"). Never pass
    /// user-controlled values.
    /// </summary>
    public static async Task<RowSet> MeasuredExecuteAsync(
        this IScyllaSession scylla,
        IStatement statement,
        string op)
    {
        MetricsCatalog.ScyllaInflight.WithLabels(op).Inc();
        var sw = Stopwatch.StartNew();
        try
        {
            return await scylla.Session.ExecuteAsync(statement).ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            MetricsCatalog.ScyllaInflight.WithLabels(op).Dec();
            MetricsCatalog.ScyllaQueryDurationSeconds.WithLabels(op).Observe(sw.Elapsed.TotalSeconds);
        }
    }
}
