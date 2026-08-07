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

    public async Task AddBatchAsync(IReadOnlyList<QuarantinedSale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0)
        {
            return;
        }

        using var _ = LogChannelScope.Begin(logger, LogChannels.ScyllaSales);

        var stmt = await scylla.PrepareAsync(CqlInsertQuarantined, ct).ConfigureAwait(false);

        var bind = (BatchStatement batch, QuarantinedSale q) =>
        {
            var s = q.Sale;
            batch.Add(stmt.Bind(
                s.ItemId, s.WorldId, s.SaleTime, s.BuyerName,
                s.Hq, s.OnMannequin, s.Quantity, s.UnitPrice,
                (long)s.Quantity * s.UnitPrice,
                q.Reason, q.BaselineMedian, q.QuarantinedAt));
        };

        foreach (var partition in sales.GroupBy(q => (q.Sale.ItemId, q.Sale.WorldId)))
        {
            ct.ThrowIfCancellationRequested();
            await ScyllaBatchWriter.ExecuteBatchedAsync(scylla, partition, bind, "quarantine_insert", ct).ConfigureAwait(false);
        }

        logger.LogInformation("Quarantined {Count} sale(s).", sales.Count);
    }
}
