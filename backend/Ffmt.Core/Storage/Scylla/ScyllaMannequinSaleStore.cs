using Cassandra;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaMannequinSaleStore(IScyllaSession scylla) : IMannequinSaleStore
{
    private const string CqlGetByDay = """
        SELECT world_id, sale_time, item_id, buyer_name, hq, quantity, unit_price
        FROM mannequin_sales_by_day
        WHERE day = ? AND sale_time < ?
        """;

    private const string CqlGetAll = """
        SELECT world_id, sale_time, item_id, buyer_name, hq, quantity, unit_price
        FROM mannequin_sales
        LIMIT ?
        """;

    private const string CqlGetDays = """
        SELECT day FROM mannequin_sales_days WHERE bucket = ?
        """;

    private const string CqlCount = """
        SELECT COUNT(*) FROM mannequin_sales WHERE world_id = ? AND day = ?
        """;

    private const string CqlDeletePartition = """
        DELETE FROM mannequin_sales WHERE world_id = ? AND day = ?
        """;

    private const string CqlDeleteDay = """
        DELETE FROM mannequin_sales_days WHERE bucket = ? AND day = ?
        """;

    public async Task AddAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        var mannequin = sales.Where(s => s.OnMannequin).ToList();
        if (mannequin.Count == 0)
        {
            return;
        }

        var saleStmt = await scylla.PrepareAsync(MannequinCql.InsertSale, ct).ConfigureAwait(false);
        var dayStmt = await scylla.PrepareAsync(MannequinCql.InsertDay, ct).ConfigureAwait(false);

        foreach (var partition in mannequin.GroupBy(s => (s.WorldId, Day: MannequinCql.DayOf(s.SaleTime))))
        {
            await ScyllaBatchWriter.ExecuteBatchedAsync(
                scylla, partition, (batch, s) => batch.Add(MannequinCql.BindSale(saleStmt, s)), "mannequin_insert", ct)
                .ConfigureAwait(false);
        }

        var oneSalePerDay = mannequin.GroupBy(s => MannequinCql.DayOf(s.SaleTime)).Select(g => g.First());
        await ScyllaBatchWriter.ExecuteBatchedAsync(
            scylla, oneSalePerDay, (batch, s) => batch.Add(MannequinCql.BindDay(dayStmt, s)), "mannequin_insert", ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Sale>> GetByDayAsync(DateOnly day, DateTimeOffset before, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetByDay, ct).ConfigureAwait(false);
        var rows = await scylla.MeasuredExecuteAsync(
            stmt.Bind(MannequinCql.ToLocalDate(day), before), "mannequin_read").ConfigureAwait(false);

        return rows.Select(ToSale).ToList();
    }

    public async Task<IReadOnlyList<Sale>> GetAllAsync(int limit, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetAll, ct).ConfigureAwait(false);
        var sales = new List<Sale>();
        byte[]? pagingState = null;

        // Scylla can end a page early by size, so page by hand rather than let enumeration block on a fetch.
        do
        {
            var page = await scylla.MeasuredExecuteAsync(
                stmt.Bind(limit).SetPageSize(limit).SetAutoPage(false).SetPagingState(pagingState), "mannequin_read_all")
                .ConfigureAwait(false);
            sales.AddRange(page.Select(ToSale));
            pagingState = page.PagingState;
        }
        while (pagingState is not null);

        return sales;
    }

    public async Task<IReadOnlyList<DateOnly>> GetDaysAsync(CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlGetDays, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(MannequinCql.DaysBucket)).ConfigureAwait(false);

        return rows
            .Select(row => row.GetValue<LocalDate>("day"))
            .Select(d => new DateOnly(d.Year, d.Month, d.Day))
            .ToList();
    }

    public async Task<long> CountAsync(int worldId, DateOnly day, CancellationToken ct = default)
    {
        var stmt = await scylla.PrepareAsync(CqlCount, ct).ConfigureAwait(false);
        var rows = await scylla.MeasuredExecuteAsync(
            stmt.Bind(worldId, MannequinCql.ToLocalDate(day)), "mannequin_count").ConfigureAwait(false);
        return rows.First().GetValue<long>("count");
    }

    public async Task DeleteDayAsync(IReadOnlyCollection<int> worldIds, DateOnly day, CancellationToken ct = default)
    {
        var partitionStmt = await scylla.PrepareAsync(CqlDeletePartition, ct).ConfigureAwait(false);
        var dayStmt = await scylla.PrepareAsync(CqlDeleteDay, ct).ConfigureAwait(false);
        var localDate = MannequinCql.ToLocalDate(day);

        await ScyllaBatchWriter.ExecuteBatchedAsync(
            scylla, worldIds, (batch, worldId) => batch.Add(partitionStmt.Bind(worldId, localDate)), "mannequin_delete", ct)
            .ConfigureAwait(false);

        // Index row last: a crash before this leaves the day listed, so the next pass retries it.
        await scylla.MeasuredExecuteAsync(dayStmt.Bind(MannequinCql.DaysBucket, localDate), "mannequin_delete")
            .ConfigureAwait(false);
    }

    private static Sale ToSale(Row row) => new(
        ItemId: row.GetValue<int>("item_id"),
        WorldId: row.GetValue<int>("world_id"),
        BuyerName: row.GetValue<string>("buyer_name") ?? string.Empty,
        Hq: row.SafeBool("hq"),
        OnMannequin: true,
        Quantity: row.SafeInt("quantity"),
        UnitPrice: row.SafeInt("unit_price"),
        SaleTime: row.GetValue<DateTimeOffset>("sale_time"));
}
