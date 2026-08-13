using Ffmt.Core.Storage.Scylla;
using Ffmt.Tests.Fakes;

namespace Ffmt.Tests.Storage.Scylla;

// Freezes the generated CQL: the capturing session records each prepare then fails, hence the empty catches.
public sealed class GilfluxRankingStoreCqlTests
{
    private static (ScyllaGilfluxRankingStore Store, List<string> Captured) NewStore()
    {
        var (session, captured) = CapturingScyllaSession.New();
        return (new ScyllaGilfluxRankingStore(session), captured);
    }

    [Fact]
    public async Task GetByWorldAsync_PreparesAgainstGilfluxRankings()
    {
        var (store, captured) = NewStore();
        try { await store.GetByWorldAsync(21); } catch { }

        captured.Should().Contain(c => c.Contains("FROM gilflux_rankings") && c.Contains("WHERE world_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM gilflux_rankings") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task GetByItemAndWorldAsync_UsesWorldFirstPrefixLookup()
    {
        var (store, captured) = NewStore();
        try { await store.GetByItemAndWorldAsync(12345, 21); } catch { }

        captured.Should().Contain(c => c.Contains("FROM gilflux_rankings") && c.Contains("WHERE world_id = ?") && c.Contains("AND item_id = ?"));
        captured.Should().NotContain(c => c.Contains("FROM gilflux_rankings") && c.Contains("ALLOW FILTERING"));
    }

    [Fact]
    public async Task DeleteManyAsync_PreparesAPrimaryKeyScopedDelete()
    {
        var (store, captured) = NewStore();
        try { await store.DeleteManyAsync([(21, 12345)]); } catch { }

        captured.Should().Contain(c =>
            c.Contains("DELETE FROM gilflux_rankings") &&
            c.Contains("WHERE world_id = ?") &&
            c.Contains("AND item_id = ?"));
    }

    [Fact]
    public async Task DeleteManyAsync_IsANoOpForAnEmptyBatch()
    {
        var (store, captured) = NewStore();

        await store.DeleteManyAsync([]);

        captured.Should().BeEmpty();
    }
}
