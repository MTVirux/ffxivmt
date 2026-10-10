using Cassandra;
using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

/// <summary>Shared by ingest (ScyllaSaleStore) and backfill (ScyllaMannequinSaleStore) so the two writes can't drift.</summary>
internal static class MannequinCql
{
    public const int DaysBucket = 0;

    public const string InsertSale = """
        INSERT INTO mannequin_sales
            (world_id, day, sale_time, item_id, buyer_name, hq, quantity, unit_price)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?)
        """;

    public const string InsertDay = """
        INSERT INTO mannequin_sales_days (bucket, day) VALUES (?, ?)
        """;

    public const string DeleteSaleExact = """
        DELETE FROM mannequin_sales
        WHERE world_id = ? AND day = ? AND sale_time = ? AND item_id = ? AND buyer_name = ?
        """;

    public static DateOnly DayOf(DateTimeOffset saleTime) => DateOnly.FromDateTime(saleTime.UtcDateTime);

    public static LocalDate ToLocalDate(DateOnly day) => new(day.Year, day.Month, day.Day);

    public static BoundStatement BindSale(PreparedStatement stmt, Sale s) =>
        stmt.Bind(s.WorldId, ToLocalDate(DayOf(s.SaleTime)), s.SaleTime, s.ItemId, s.BuyerName, s.Hq, s.Quantity, s.UnitPrice);

    public static BoundStatement BindDay(PreparedStatement stmt, Sale s) =>
        stmt.Bind(DaysBucket, ToLocalDate(DayOf(s.SaleTime)));

    public static BoundStatement BindDeleteExact(PreparedStatement stmt, Sale s) =>
        stmt.Bind(s.WorldId, ToLocalDate(DayOf(s.SaleTime)), s.SaleTime, s.ItemId, s.BuyerName);
}
