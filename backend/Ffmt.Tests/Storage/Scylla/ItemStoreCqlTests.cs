using Cassandra;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class ItemStoreCqlTests
{
    private static (ScyllaItemStore Store, List<string> Captured) NewStore()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (new ScyllaItemStore(session), captured);
    }

    [Fact]
    public async Task GetMarketableIdsAsync_QueriesItemSets_NoAllowFiltering()
    {
        var (store, captured) = NewStore();
        try { await store.GetMarketableIdsAsync(); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("FROM item_sets") && c.Contains("WHERE set_name = ?"));
        captured.Should().NotContain(c => c.Contains("FROM item_sets") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task GetCraftableIdsAsync_QueriesItemSets_NoAllowFiltering()
    {
        var (store, captured) = NewStore();
        try { await store.GetCraftableIdsAsync(); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("FROM item_sets") && c.Contains("WHERE set_name = ?"));
        captured.Should().NotContain(c => c.Contains("FROM item_sets") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task UpdateMarketableAsync_True_PreparesInsertIntoItemSets()
    {
        var (store, captured) = NewStore();
        try { await store.UpdateMarketableAsync(12345, marketable: true); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("UPDATE items SET marketable = ?"));
        captured.Should().Contain(c => c.Contains("INSERT INTO item_sets"));
    }

    [Fact]
    public async Task UpdateMarketableAsync_False_PreparesDeleteFromItemSets()
    {
        var (store, captured) = NewStore();
        try { await store.UpdateMarketableAsync(12345, marketable: false); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("DELETE FROM item_sets"));
    }
}
