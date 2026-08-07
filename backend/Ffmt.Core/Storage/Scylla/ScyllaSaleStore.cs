using System.Diagnostics;
using Cassandra;
using Ffmt.Core.Logging;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Quarantine;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaSaleStore(IScyllaSession scylla, ILogger<ScyllaSaleStore> logger) : ISaleStore
{
    private const string CqlInsertSale = """
        INSERT INTO sales
            (item_id, world_id, sale_time, buyer_name, hq, on_mannequin, quantity, unit_price, total_price, total_price_gil)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """;

    private const string CqlInsertSaleByBuyer = """
        INSERT INTO sales_by_buyer
            (buyer_name, world_id, sale_time, item_id)
        VALUES (?, ?, ?, ?)
        """;

    // Reads from sales_by_buyer; the API caller does follow-up reads on `sales`
    // for full sale fields if needed.
    private const string CqlSearchBuyer = """
        SELECT buyer_name, sale_time, item_id, world_id
        FROM sales_by_buyer
        WHERE buyer_name = ?
        """;

    private const string CqlSearchBuyerWithWorld = """
        SELECT buyer_name, sale_time, item_id, world_id
        FROM sales_by_buyer
        WHERE buyer_name = ? AND world_id = ?
        """;

    private const string CqlGetByItemAndWorld = """
        SELECT item_id, world_id, sale_time, buyer_name, hq, on_mannequin, quantity, unit_price
        FROM sales
        WHERE item_id = ? AND world_id = ?
        ORDER BY sale_time DESC
        LIMIT ?
        """;

    private const string CqlGetByItemAndWorldInRange = """
        SELECT item_id, world_id, sale_time, buyer_name, hq, on_mannequin, quantity, unit_price
        FROM sales
        WHERE item_id = ? AND world_id = ?
          AND sale_time >= ? AND sale_time < ?
        ORDER BY sale_time ASC
        """;

    private const string CqlDeleteSalesInRange = """
        DELETE FROM sales
        WHERE item_id = ? AND world_id = ?
          AND sale_time >= ? AND sale_time < ?
        """;

    private const string CqlDeleteSaleByBuyer = """
        DELETE FROM sales_by_buyer
        WHERE buyer_name = ? AND world_id = ? AND sale_time = ?
        """;

    private const string CqlGetPricePointsSince = """
        SELECT hq, unit_price
        FROM sales
        WHERE item_id = ? AND world_id = ? AND sale_time >= ?
        """;

    private const string CqlDeleteSaleExact = """
        DELETE FROM sales
        WHERE item_id = ? AND world_id = ? AND sale_time = ? AND buyer_name = ?
        """;

    private const string CqlUpdateTotalPriceGil = """
        UPDATE sales SET total_price_gil = ?
        WHERE item_id = ? AND world_id = ? AND sale_time = ? AND buyer_name = ?
        """;

    private readonly RequestCoalescer<(int ItemId, int WorldId, int Limit), IReadOnlyList<Sale>> _readCoalescer = new();

    public async Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0)
        {
            return new SaleBatchResult(0, 0d);
        }

        using var _ = LogChannelScope.Begin(logger, LogChannels.ScyllaSales);

        var saleStmt = await scylla.PrepareAsync(CqlInsertSale, ct).ConfigureAwait(false);
        var byBuyerStmt = await scylla.PrepareAsync(CqlInsertSaleByBuyer, ct).ConfigureAwait(false);
        var sw = Stopwatch.StartNew();
        var parsed = 0;

        var bind = (BatchStatement batch, Sale s) =>
        {
            var totalPrice = (long)s.Quantity * s.UnitPrice;
            batch.Add(saleStmt.Bind(
                s.ItemId, s.WorldId, s.SaleTime, s.BuyerName,
                s.Hq, s.OnMannequin, s.Quantity, s.UnitPrice,
                // total_price is the legacy int column, clamped rather than wrapped until it is dropped.
                (int)Math.Min(totalPrice, int.MaxValue), totalPrice));
            batch.Add(byBuyerStmt.Bind(
                s.BuyerName, s.WorldId, s.SaleTime, s.ItemId));
            parsed++;
        };

        // Group by sales partition key so single-partition unlogged batches stay one-coordinator.
        // sales_by_buyer rows go into the same batch as their parent sale — they may target
        // different partitions, but at this batch size (≤200) the coordinator overhead is
        // acceptable and atomic-per-sale write semantics are preserved.
        foreach (var partition in sales.GroupBy(s => (s.ItemId, s.WorldId)))
        {
            await ScyllaBatchWriter.ExecuteBatchedAsync(scylla, partition, bind, "sale_insert", ct).ConfigureAwait(false);
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

        // Each row in sales_by_buyer holds only the keys; we return Sale records with
        // the buyer/time/item/world keys filled and the remaining fields zeroed. Callers
        // that need full sale fields can fan out to GetByItemAndWorldAsync for each pair,
        // but the current SearchBuyer endpoint only displays the buyer-keyed projection.
        var result = new List<Sale>();
        foreach (var row in rows)
        {
            result.Add(new Sale(
                ItemId: row.GetValue<int>("item_id"),
                WorldId: row.GetValue<int>("world_id"),
                BuyerName: row.GetValue<string>("buyer_name") ?? string.Empty,
                Hq: false,
                OnMannequin: false,
                Quantity: 0,
                UnitPrice: 0,
                SaleTime: row.GetValue<DateTimeOffset>("sale_time")));
        }
        return result;
    }

    public Task<IReadOnlyList<Sale>> GetByItemAndWorldAsync(int itemId, int worldId, int limit, CancellationToken ct = default)
    {
        return _readCoalescer.CoalesceAsync(
            (itemId, worldId, limit),
            () => FetchByItemAndWorldAsync(itemId, worldId, limit));
    }

    private async Task<IReadOnlyList<Sale>> FetchByItemAndWorldAsync(int itemId, int worldId, int limit)
    {
        var stmt = await scylla.PrepareAsync(CqlGetByItemAndWorld).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(itemId, worldId, limit)).ConfigureAwait(false);

        var result = new List<Sale>(capacity: Math.Min(limit, 256));
        foreach (var row in rows)
        {
            result.Add(MapSaleRow(row));
        }
        return result;
    }

    public async Task<IReadOnlyList<Sale>> GetByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);
        var stmt = await scylla.PrepareAsync(CqlGetByItemAndWorldInRange, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(itemId, worldId, start, end)).ConfigureAwait(false);
        return rows.Select(MapSaleRow).ToList();
    }

    public async Task DeleteByItemAndWorldInRangeAsync(
        int itemId, int worldId, DateOnly date, IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        var start = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);

        var rangeStmt = await scylla.PrepareAsync(CqlDeleteSalesInRange, ct).ConfigureAwait(false);
        var buyerStmt = await scylla.PrepareAsync(CqlDeleteSaleByBuyer, ct).ConfigureAwait(false);

        await scylla.Session.ExecuteAsync(rangeStmt.Bind(itemId, worldId, start, end)).ConfigureAwait(false);

        if (sales.Count == 0) return;

        // One batch per buyer partition; these groups are far below the batch-row flush point.
        foreach (var group in sales.GroupBy(s => s.BuyerName))
        {
            ct.ThrowIfCancellationRequested();
            var batch = ScyllaBatchWriter.NewBatch();
            foreach (var s in group)
                batch.Add(buyerStmt.Bind(s.BuyerName, s.WorldId, s.SaleTime));
            await scylla.MeasuredExecuteAsync(batch, "sale_delete").ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<PricePoint>> GetPricePointsSinceAsync(
        int itemId, int worldId, DateTimeOffset since, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetPricePointsSince, ct).ConfigureAwait(false);
        var rows = await scylla.MeasuredExecuteAsync(
            stmt.Bind(itemId, worldId, since), "price_points_read").ConfigureAwait(false);

        var result = new List<PricePoint>();
        foreach (var row in rows)
        {
            result.Add(new PricePoint(
                row.SafeBool("hq"),
                row.GetValue<int>("unit_price")));
        }
        return result;
    }

    public async Task DeleteExactAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0) return;

        var saleStmt = await scylla.PrepareAsync(CqlDeleteSaleExact, ct).ConfigureAwait(false);
        var buyerStmt = await scylla.PrepareAsync(CqlDeleteSaleByBuyer, ct).ConfigureAwait(false);

        var bind = (BatchStatement batch, Sale s) =>
        {
            batch.Add(saleStmt.Bind(s.ItemId, s.WorldId, s.SaleTime, s.BuyerName));
            batch.Add(buyerStmt.Bind(s.BuyerName, s.WorldId, s.SaleTime));
        };

        foreach (var partition in sales.GroupBy(s => (s.ItemId, s.WorldId)))
        {
            ct.ThrowIfCancellationRequested();
            await ScyllaBatchWriter.ExecuteBatchedAsync(scylla, partition, bind, "sale_delete", ct).ConfigureAwait(false);
        }
    }

    public async Task BackfillTotalPriceAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        if (sales.Count == 0) return;

        var stmt = await scylla.PrepareAsync(CqlUpdateTotalPriceGil, ct).ConfigureAwait(false);

        foreach (var partition in sales.GroupBy(s => (s.ItemId, s.WorldId)))
        {
            ct.ThrowIfCancellationRequested();
            await ScyllaBatchWriter.ExecuteBatchedAsync(
                scylla,
                partition,
                (batch, s) => batch.Add(stmt.Bind((long)s.Quantity * s.UnitPrice, s.ItemId, s.WorldId, s.SaleTime, s.BuyerName)),
                "sale_backfill",
                ct).ConfigureAwait(false);
        }
    }

    private static Sale MapSaleRow(Row row) => new(
        ItemId:      row.GetValue<int>("item_id"),
        WorldId:     row.GetValue<int>("world_id"),
        BuyerName:   row.GetValue<string>("buyer_name") ?? string.Empty,
        Hq:          row.SafeBool("hq"),
        OnMannequin: row.SafeBool("on_mannequin"),
        Quantity:    row.GetValue<int>("quantity"),
        UnitPrice:   row.GetValue<int>("unit_price"),
        SaleTime:    row.GetValue<DateTimeOffset>("sale_time"));
}
