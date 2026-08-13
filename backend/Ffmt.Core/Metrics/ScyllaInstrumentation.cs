using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Storage.Scylla;

namespace Ffmt.Core.Metrics;

public static class ScyllaInstrumentation
{
    // op is a metric label, so it must be a low-cardinality constant ("sale_insert",
    // "gilflux_refresh", ...). Never pass a user-controlled value.
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
