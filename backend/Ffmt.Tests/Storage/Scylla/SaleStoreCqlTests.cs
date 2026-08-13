using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ffmt.Tests.Storage.Scylla;

// Freezes the generated CQL: the capturing session records each prepare then fails, hence the empty catches.
public sealed class SaleStoreCqlTests
{
    [Fact]
    public async Task AddBatchAsync_PreparesSalesInsertWithoutDerivableColumns()
    {
        var (store, capturedCql) = NewStore();

        try { await store.AddBatchAsync(Array.Empty<Ffmt.Core.Models.Sale>()); }
        catch { }

        capturedCql.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchBuyerAsync_PreparesCqlAgainstSalesByBuyerCompanion()
    {
        var (store, captured) = NewStore();
        try { await store.SearchBuyerAsync("Alice", null); } catch { }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM sales_by_buyer")
            .And.Contain("WHERE buyer_name = ?")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task SearchBuyerAsync_SelectsQuantityAndUnitPrice()
    {
        var (store, captured) = NewStore();
        try { await store.SearchBuyerAsync("Alice", null); } catch { }

        captured.Should().ContainSingle()
            .Which.Should().Contain("quantity")
            .And.Contain("unit_price");
    }

    [Fact]
    public async Task SearchBuyerAsync_WithWorld_UsesWorldPrefixLookup()
    {
        var (store, captured) = NewStore();
        try { await store.SearchBuyerAsync("Alice", worldId: 21); } catch { }

        captured.Should().Contain(c => c.Contains("FROM sales_by_buyer") && c.Contains("AND world_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM sales_by_buyer") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task AddBatchAsync_writes_the_bigint_total_alongside_the_legacy_int_column()
    {
        var (store, captured) = NewStore();
        var sale = new Ffmt.Core.Models.Sale(2, 21, "Alisaie", false, false, 1, 100,
            new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        try { await store.AddBatchAsync([sale]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("INSERT INTO sales") &&
            c.Contains("total_price") &&
            c.Contains("total_price_gil"));
    }

    [Fact]
    public async Task AddBatchAsync_writes_quantity_and_unit_price_into_the_buyer_companion()
    {
        var (store, captured) = NewStore();
        var sale = new Ffmt.Core.Models.Sale(2, 21, "Alisaie", false, false, 3, 100,
            new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        try { await store.AddBatchAsync([sale]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("INSERT INTO sales_by_buyer") &&
            c.Contains("quantity") &&
            c.Contains("unit_price"));
    }

    private static (ScyllaSaleStore Store, List<string> Captured) NewStore()
    {
        var (session, captured) = CapturingScyllaSession.New();
        return (new ScyllaSaleStore(session, NullLogger<ScyllaSaleStore>.Instance), captured);
    }
}
