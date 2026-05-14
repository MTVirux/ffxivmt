using Cassandra;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class SaleStoreRangeCqlTests
{
    [Fact]
    public async Task GetByItemAndWorldInRangeAsync_QueriesSalesByPartitionKeyAndDateRange()
    {
        var (store, captured) = NewStore();

        try { await store.GetByItemAndWorldInRangeAsync(2, 21, new DateOnly(2026, 5, 1)); } catch { }

        captured.Should().Contain(c =>
            c.Contains("FROM sales") &&
            c.Contains("item_id = ?") &&
            c.Contains("world_id = ?") &&
            c.Contains("sale_time >=") &&
            c.Contains("sale_time <"));
    }

    [Fact]
    public async Task DeleteByItemAndWorldInRangeAsync_DeletesFromSalesWithRangeAndFromSalesByBuyer()
    {
        var (store, captured) = NewStore();
        var sale = new Ffmt.Core.Models.Sale(2, 21, "Alisaie", false, false, 1, 100,
            new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));

        try { await store.DeleteByItemAndWorldInRangeAsync(2, 21, new DateOnly(2026, 5, 1), [sale]); } catch { }

        captured.Should().Contain(c => c.Contains("DELETE") && c.Contains("FROM sales") && c.Contains("sale_time >="));
        captured.Should().Contain(c => c.Contains("DELETE") && c.Contains("FROM sales_by_buyer"));
    }

    private static (ScyllaSaleStore Store, List<string> Captured) NewStore()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (new ScyllaSaleStore(session, NullLogger<ScyllaSaleStore>.Instance), captured);
    }
}
