using Cassandra;
using Ffmt.Core.Metrics;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>Single home for the unlogged-batch-at-LocalOne write pattern the Scylla stores share.</summary>
internal static class ScyllaBatchWriter
{
    public const int BatchRows = 200;

    public static BatchStatement NewBatch() =>
        (BatchStatement)new BatchStatement()
            .SetBatchType(BatchType.Unlogged)
            .SetConsistencyLevel(ConsistencyLevel.LocalOne);

    /// <summary>
    /// Adds every row to an unlogged batch, flushing each <paramref name="batchRows"/> rows.
    /// Callers group by partition key themselves and call once per group. A non-null
    /// <paramref name="op"/> routes the execute through the metrics wrapper.
    /// </summary>
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
