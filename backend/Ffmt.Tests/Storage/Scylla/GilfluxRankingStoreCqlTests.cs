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
}
