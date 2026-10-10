using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class MannequinSaleStoreCqlTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Day = new(2026, 10, 10);

    private static (ScyllaMannequinSaleStore Store, List<string> Captured) NewStore()
    {
        var (session, captured) = CapturingScyllaSession.New();
        return (new ScyllaMannequinSaleStore(session), captured);
    }

    private static Sale NewSale(bool onMannequin) => new(2, 21, "Alisaie", false, onMannequin, 1, 100, Noon);

    [Fact]
    public async Task AddAsync_writes_the_sale_and_its_day()
    {
        var (store, captured) = NewStore();

        try { await store.AddAsync([NewSale(onMannequin: true)]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("INSERT INTO mannequin_sales") &&
            c.Contains("(world_id, day, sale_time, item_id, buyer_name, hq, quantity, unit_price)"));
        captured.Should().Contain(c => c.Contains("INSERT INTO mannequin_sales_days"));
    }

    [Fact]
    public async Task AddAsync_skips_sales_not_on_a_mannequin()
    {
        var (store, captured) = NewStore();

        await store.AddAsync([NewSale(onMannequin: false)]);

        captured.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByDayAsync_reads_one_view_partition_before_the_cursor()
    {
        var (store, captured) = NewStore();

        try { await store.GetByDayAsync(Day, Noon); } catch { }

        captured.Should().Contain(c =>
            c.Contains("FROM mannequin_sales_by_day") &&
            c.Contains("WHERE day = ? AND sale_time < ?") &&
            !c.Contains("world_id = ?") &&
            !c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task GetDaysAsync_reads_the_day_index()
    {
        var (store, captured) = NewStore();

        try { await store.GetDaysAsync(); } catch { }

        captured.Should().Contain(c => c.Contains("SELECT day FROM mannequin_sales_days") && c.Contains("bucket = ?"));
    }

    [Fact]
    public async Task CountAsync_counts_one_partition()
    {
        var (store, captured) = NewStore();

        try { await store.CountAsync(21, Day); } catch { }

        captured.Should().Contain(c =>
            c.Contains("SELECT COUNT(*) FROM mannequin_sales") &&
            c.Contains("world_id = ? AND day = ?"));
    }

    [Fact]
    public async Task DeleteDayAsync_drops_whole_partitions_and_the_index_row()
    {
        var (store, captured) = NewStore();

        try { await store.DeleteDayAsync([21, 22], Day); } catch { }

        captured.Should().Contain(c =>
            c.Contains("DELETE FROM mannequin_sales") &&
            c.Contains("world_id = ? AND day = ?") &&
            !c.Contains("sale_time"));
        captured.Should().Contain(c => c.Contains("DELETE FROM mannequin_sales_days") && c.Contains("bucket = ? AND day = ?"));
    }
}
