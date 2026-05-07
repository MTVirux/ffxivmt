using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>
/// Thin client over <c>universalis.app/api/v2</c>. Used by the <c>updatedb</c> CLI to
/// rebuild the marketability flag and the worlds/datacenters topology.
/// </summary>
public interface IUniversalisClient
{
    Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<World>> GetAllWorldsAsync(CancellationToken ct = default);
}
