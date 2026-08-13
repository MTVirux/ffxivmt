using Cassandra;
using Ffmt.Core.Metrics;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>The only place that builds a BatchStatement (unlogged, LocalOne). Every store batches
/// through ExecuteBatchedAsync instead of hand-rolling a chunk-and-flush loop.</summary>
internal static class ScyllaBatchWriter
{
    public const int BatchRows = 200;

    public static BatchStatement NewBatch() =>
        (BatchStatement)new BatchStatement()
            .SetBatchType(BatchType.Unlogged)
            .SetConsistencyLevel(ConsistencyLevel.LocalOne);

    /// <summary>Callers group by partition key themselves and call once per group.</summary>
    public static async Task ExecuteBatchedAsync<T>(
        IScyllaSession scylla,
        IEnumerable<T> rows,
        Action<BatchStatement, T> bind,
        string? op,
        CancellationToken ct,
        int batchRows = BatchRows)
    {
        var batch = NewBatch();
        var inBatch = 0;

        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            bind(batch, row);
            inBatch++;

            if (inBatch == batchRows)
            {
                await ExecuteAsync(scylla, batch, op).ConfigureAwait(false);
                batch = NewBatch();
                inBatch = 0;
            }
        }

        if (inBatch > 0)
        {
            await ExecuteAsync(scylla, batch, op).ConfigureAwait(false);
        }
    }

    private static Task ExecuteAsync(IScyllaSession scylla, BatchStatement batch, string? op) =>
        op is null
            ? scylla.Session.ExecuteAsync(batch)
            : scylla.MeasuredExecuteAsync(batch, op);
}
