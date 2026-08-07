using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class SaleStoreScrubCqlTests
{
    private static (ScyllaSaleStore Store, List<string> Captured) NewStore()
    {
        var (session, captured) = CapturingScyllaSession.New();
        return (new ScyllaSaleStore(session, NullLogger<ScyllaSaleStore>.Instance), captured);
    }

    private static Sale NewSale() => new(2, 21, "Alisaie", false, false, 3, 100,
        new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task GetPricePointsSinceAsync_projects_only_hq_and_unit_price()
    {
        var (store, captured) = NewStore();

        try { await store.GetPricePointsSinceAsync(2, 21, DateTimeOffset.UnixEpoch); } catch { }

        captured.Should().Contain(c =>
            c.Contains("SELECT hq, unit_price") &&
            c.Contains("FROM sales") &&
            c.Contains("item_id = ?") &&
            c.Contains("world_id = ?") &&
            c.Contains("sale_time >= ?"));
        captured.Should().NotContain(c => c.Contains("SELECT *"),
            "the baseline job touches millions of partitions - it must not materialise full rows");
    }

    [Fact]
    public async Task DeleteExactAsync_targets_the_full_primary_key_in_both_tables()
    {
        var (store, captured) = NewStore();

        try { await store.DeleteExactAsync([NewSale()]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("DELETE FROM sales") &&
            c.Contains("sale_time = ?") &&
            c.Contains("buyer_name = ?") &&
            !c.Contains("sale_time >="));
        captured.Should().Contain(c => c.Contains("DELETE FROM sales_by_buyer"));
    }

    [Fact]
    public async Task BackfillTotalPriceAsync_updates_only_the_bigint_column()
    {
        var (store, captured) = NewStore();

        try { await store.BackfillTotalPriceAsync([NewSale()]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("UPDATE sales") &&
            c.Contains("SET total_price_gil = ?") &&
            c.Contains("buyer_name = ?"));
    }
}
