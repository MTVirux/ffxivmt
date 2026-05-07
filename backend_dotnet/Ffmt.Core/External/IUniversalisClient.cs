using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>
/// Thin client over <c>universalis.app/api/v2</c>. Used by the <c>updatedb</c> CLI to
/// rebuild the marketability flag and the worlds/datacenters topology, and by the
/// profit-calculator tools to fetch market board summaries.
/// </summary>
public interface IUniversalisClient
{
    Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<World>> GetAllWorldsAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches market-board summaries (<c>minPrice</c>, <c>regularSaleVelocity</c>) for the
    /// given <paramref name="itemIds"/> at <paramref name="location"/> (world name, datacenter
    /// name, or region name). Returns one entry per item id Universalis answered for; ids the
    /// upstream omits are silently dropped.
    /// </summary>
    Task<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> GetMarketBoardDataAsync(
        string location, IReadOnlyList<int> itemIds, CancellationToken ct = default);
}

public sealed record UniversalisMarketBoardListing(int MinPrice, double RegularSaleVelocity);
