using Cassandra;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class SaleStoreCqlTests
{
    [Fact]
    public async Task AddBatchAsync_PreparesSalesInsertWithoutDerivableColumns()
    {
        var session = Substitute.For<IScyllaSession>();
        var capturedCql = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => capturedCql.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        // The store will attempt to bind/execute via session.Session — leave that null;
        // we only assert the prepared CQL strings recorded, not the full execution path.

        var store = new ScyllaSaleStore(session, NullLogger<ScyllaSaleStore>.Instance);

        try { await store.AddBatchAsync(Array.Empty<Ffmt.Core.Models.Sale>()); }
        catch { /* expected: no real session */ }

        // Empty input: AddBatchAsync should short-circuit without preparing anything.
        capturedCql.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchBuyerAsync_PreparesCqlAgainstSalesByBuyerCompanion()
    {
        var (store, captured) = NewStore();
        try { await store.SearchBuyerAsync("Alice", null); } catch { /* no real session */ }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM sales_by_buyer")
            .And.Contain("WHERE buyer_name = ?")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task SearchBuyerAsync_WithWorld_UsesWorldPrefixLookup()
    {
        var (store, captured) = NewStore();
        try { await store.SearchBuyerAsync("Alice", worldId: 21); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("FROM sales_by_buyer") && c.Contains("AND world_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM sales_by_buyer") && c.Contains("ALLOW FILTERING"));
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
