using Cassandra;
using Ffmt.Core.Logging;
using Ffmt.Core.Metrics;
using Ffmt.Core.Quarantine;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaQuarantineStore(IScyllaSession scylla, ILogger<ScyllaQuarantineStore> logger)
    : IQuarantineStore
{
    private const string CqlInsertQuarantined = """
        INSERT INTO sales_quarantine
            (item_id, world_id, sale_time, buyer_name, hq, on_mannequin, quantity, unit_price,
             total_price, reason, baseline_median, quarantined_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const int BatchRows = 200;

    public async Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0)
        {
            return;
        }

        using var _ = logger.BeginScope(new Dictionary<string, object>
        {
            [LogChannels.ContextPropertyName] = LogChannels.ScyllaSales,
        });

        var stmt = await scylla.PrepareAsync(CqlInsertQuarantined, ct).ConfigureAwait(false);

        foreach (var partition in sales.GroupBy(q => (q.Sale.ItemId, q.Sale.WorldId)))
        {
            ct.ThrowIfCancellationRequested();
            var batch = NewBatch();
            var inBatch = 0;

            foreach (var q in partition)
            {
                var s = q.Sale;
                batch.Add(stmt.Bind(
                    s.ItemId, s.WorldId, s.SaleTime, s.BuyerName,
                    s.Hq, s.OnMannequin, s.Quantity, s.UnitPrice,
                    (long)s.Quantity * s.UnitPrice,
                    q.Reason, q.BaselineMedian, q.QuarantinedAt));
                inBatch++;

                if (inBatch == BatchRows)
                {
                    await scylla.MeasuredExecuteAsync(batch, "quarantine_insert").ConfigureAwait(false);
                    batch = NewBatch();
                    inBatch = 0;
                }
            }

            if (inBatch > 0)
            {
                await scylla.MeasuredExecuteAsync(batch, "quarantine_insert").ConfigureAwait(false);
            }
        }

        logger.LogInformation("Quarantined {Count} sale(s).", sales.Count);
    }

    private static BatchStatement NewBatch() =>
        (BatchStatement)new BatchStatement()
            .SetBatchType(BatchType.Unlogged)
            .SetConsistencyLevel(ConsistencyLevel.LocalOne);
}
