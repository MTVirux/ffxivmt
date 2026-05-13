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
    public async Task GetByWorldAsync_PreparesAgainstGilfluxRankings()
    {
        var (store, captured) = NewStore();
        try { await store.GetByWorldAsync(21); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("FROM gilflux_rankings") && c.Contains("WHERE world_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM gilflux_rankings") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task GetByItemAndWorldAsync_UsesWorldFirstPrefixLookup()
    {
        var (store, captured) = NewStore();
        try { await store.GetByItemAndWorldAsync(12345, 21); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("FROM gilflux_rankings") && c.Contains("WHERE world_id = ?") && c.Contains("AND item_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM gilflux_rankings") && c.Contains("ALLOW FILTERING"));
    }
}
