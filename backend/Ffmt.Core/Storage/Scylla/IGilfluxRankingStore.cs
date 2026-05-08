using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IGilfluxRankingStore
{
    Task<IReadOnlyList<GilfluxRanking>> GetByWorldAsync(int worldId, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByDatacenterAsync(string datacenter, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByRegionAsync(string region, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByItemAsync(int itemId, CancellationToken ct = default);

    Task<IReadOnlyList<GilfluxRanking>> GetByItemAndWorldAsync(int itemId, int worldId, CancellationToken ct = default);

    /// <summary>
    /// Recomputes the rolling-window rankings for a single <c>(world, item)</c> pair from the
    /// <c>sales</c> table and upserts a row into <c>gilflux_ranking</c>. Called by the API ingestion
    /// endpoint and by the <c>fix-gilflux-names</c> CLI subcommand.
    /// </summary>
    Task UpdateRankingAsync(int worldId, int itemId, CancellationToken ct = default);

    /// <summary>Returns one <c>item_id</c> from <c>gilflux_ranking</c> whose <c>item_name</c> is empty, or <c>null</c> if none remain.</summary>
    Task<int?> GetOneItemIdWithMissingNameAsync(CancellationToken ct = default);
}
