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

    private const string CqlSearchBuyer = """
        SELECT buyer_name, hq, on_mannequin, unit_price, quantity, sale_time,
               world_id, item_id, world_name, item_name, total, datacenter, region
        FROM sales
        WHERE buyer_name = ?
        """;

    private const string CqlSearchBuyerWithWorld = """
        SELECT buyer_name, hq, on_mannequin, unit_price, quantity, sale_time,
               world_id, item_id, world_name, item_name, total, datacenter, region
        FROM sales
        WHERE buyer_name = ? AND world_id = ?
        ALLOW FILTERING
        """;

    // sale_time is the first clustering column on ((item_id, world_id), sale_time, ...),
    // so ORDER BY DESC + LIMIT stays inside one partition.
    private const string CqlGetByItemAndWorld = """
        SELECT buyer_name, hq, on_mannequin, unit_price, quantity, sale_time,
               world_id, item_id, world_name, item_name, total, datacenter, region
        FROM sales
        WHERE item_id = ? AND world_id = ?
        ORDER BY sale_time DESC
        LIMIT ?
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

    public async Task<IReadOnlyList<Sale>> SearchBuyerAsync(string buyerName, int? worldId, CancellationToken ct = default)
    {
        var (cql, args) = worldId is null
            ? (CqlSearchBuyer, new object[] { buyerName })
            : (CqlSearchBuyerWithWorld, new object[] { buyerName, worldId.Value });

        var stmt = await scylla.PrepareAsync(cql, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(args)).ConfigureAwait(false);

        var result = new List<Sale>();
        foreach (var row in rows)
        {
            result.Add(MapRow(row));
        }
        return result;
    }

    public async Task<IReadOnlyList<Sale>> GetByItemAndWorldAsync(int itemId, int worldId, int limit, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetByItemAndWorld, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(itemId, worldId, limit)).ConfigureAwait(false);

        var result = new List<Sale>(capacity: Math.Min(limit, 256));
        foreach (var row in rows)
        {
            result.Add(MapRow(row));
        }
        return result;
    }

    private static Sale MapRow(Row row) => new(
        ItemId:       row.GetValue<int>("item_id"),
        WorldId:      row.GetValue<int>("world_id"),
        ItemName:     row.GetValue<string>("item_name") ?? string.Empty,
        WorldName:    row.GetValue<string>("world_name") ?? string.Empty,
        Datacenter:   row.GetValue<string>("datacenter") ?? string.Empty,
        Region:       row.GetValue<string>("region") ?? string.Empty,
        BuyerName:    row.GetValue<string>("buyer_name") ?? string.Empty,
        Hq:           !row.IsNull("hq") && row.GetValue<bool>("hq"),
        OnMannequin:  !row.IsNull("on_mannequin") && row.GetValue<bool>("on_mannequin"),
        Quantity:     row.GetValue<int>("quantity"),
        UnitPrice:    row.GetValue<int>("unit_price"),
        Total:        row.GetValue<int>("total"),
        SaleTime:     row.GetValue<DateTimeOffset>("sale_time"));

    private static BatchStatement NewBatch(ConsistencyLevel cl) =>
        (BatchStatement)new BatchStatement().SetBatchType(BatchType.Unlogged).SetConsistencyLevel(cl);
}
