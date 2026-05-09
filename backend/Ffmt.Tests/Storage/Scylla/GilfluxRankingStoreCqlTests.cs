using Cassandra;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class GilfluxRankingStoreCqlTests
{
    private static (ScyllaGilfluxRankingStore Store, List<string> Captured) NewStore()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (new ScyllaGilfluxRankingStore(session), captured);
    }

    [Fact]
    public async Task GetByWorldAsync_PreparesAgainstGilfluxByWorld()
    {
        var (store, captured) = NewStore();
        try { await store.GetByWorldAsync(21); } catch { /* no real session */ }

        captured.Should().ContainSingle()
            .Which.Should().Contain("FROM gilflux_by_world")
            .And.Contain("WHERE world_id = ?")
            .And.NotContain("ALLOW FILTERING");
    }

    [Fact]
    public async Task UpdateRankingAsync_PreparesBothCanonicalAndCompanionInsert()
    {
        var (store, captured) = NewStore();
        try { await store.UpdateRankingAsync(21, 12345); } catch { /* no real session */ }

        // Two INSERTs (one per table) plus the timeframe-sum and max-sale-time SELECTs.
        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_ranking"));
        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_by_world"));
        captured.Should().Contain(c => c.Contains("SUM(quantity * unit_price)"));
        captured.Should().NotContain(c => c.Contains("SUM(total)"));
        captured.Should().NotContain(c => c.Contains("ranking_alltime"));
    }
}
