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
    public async Task GetMarketableIdsAsync_UsesLookupTable_NoAllowFiltering()
    {
        var (store, captured) = NewStore();
        try { await store.GetMarketableIdsAsync(); } catch { /* no real session */ }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM marketable_items")
            .And.Contain("WHERE bucket = 0")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task GetCraftableIdsAsync_UsesLookupTable_NoAllowFiltering()
    {
        var (store, captured) = NewStore();
        try { await store.GetCraftableIdsAsync(); } catch { /* no real session */ }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM craftable_items")
            .And.Contain("WHERE bucket = 0")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task UpdateMarketableAsync_True_PreparesInsertIntoMarketableItems()
    {
        var (store, captured) = NewStore();
        try { await store.UpdateMarketableAsync(12345, marketable: true); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("UPDATE items SET marketable = ?"));
        captured.Should().Contain(c => c.Contains("INSERT INTO marketable_items"));
    }

    [Fact]
    public async Task UpdateMarketableAsync_False_PreparesDeleteFromMarketableItems()
    {
        var (store, captured) = NewStore();
        try { await store.UpdateMarketableAsync(12345, marketable: false); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("DELETE FROM marketable_items"));
    }
}
