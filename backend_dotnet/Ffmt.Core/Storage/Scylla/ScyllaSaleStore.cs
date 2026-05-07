using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Logging;
using Ffmt.Core.Models;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaSaleStore(IScyllaSession scylla, ILogger<ScyllaSaleStore> logger) : ISaleStore
{
    private const string CqlInsert = """
        INSERT INTO sales
            (buyer_name, hq, on_mannequin, quantity, sale_time, world_id, item_id,
             world_name, unit_price, item_name, datacenter, region, total)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const int BatchRows = 1000;

    public async Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0)
        {
            return new SaleBatchResult(0, 0d);
        }

        using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaSales });

        var stmt = await scylla.PrepareAsync(CqlInsert, ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();

        var batch = NewBatch(ConsistencyLevel.LocalOne);
        var inBatch = 0;
        var parsed = 0;

        for (var i = 0; i < sales.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var s = sales[i];
            batch.Add(stmt.Bind(
                s.BuyerName, s.Hq, s.OnMannequin, s.Quantity, s.SaleTime,
                s.WorldId, s.ItemId, s.WorldName, s.UnitPrice, s.ItemName,
                s.Datacenter, s.Region, s.Total));
            inBatch++;
            parsed++;

            if (inBatch == BatchRows)
            {
                await scylla.Session.ExecuteAsync(batch).ConfigureAwait(false);
                batch = NewBatch(ConsistencyLevel.LocalOne);
                inBatch = 0;
            }
        }

        if (inBatch > 0)
        {
            // Final partial batch: PHP passes `5` to `php-cql`'s `batch()`, which is CL=ALL.
            batch.SetConsistencyLevel(ConsistencyLevel.All);
            await scylla.Session.ExecuteAsync(batch).ConfigureAwait(false);
        }

        sw.Stop();
        var seconds = sw.Elapsed.TotalSeconds;
        logger.LogInformation("Inserted {Parsed} sales in {Seconds:F3}s.", parsed, seconds);
        return new SaleBatchResult(parsed, seconds);
    }

    private static BatchStatement NewBatch(ConsistencyLevel cl) =>
        (BatchStatement)new BatchStatement().SetBatchType(BatchType.Unlogged).SetConsistencyLevel(cl);
}
