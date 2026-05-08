using Ffmt.Core.Models;

namespace Ffmt.Core.External;

public interface IUniversalisClient
{
    Task<IReadOnlyList<int>> GetMarketableItemIdsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<World>> GetAllWorldsAsync(CancellationToken ct = default);

    /// <summary>Returns one entry per item id Universalis answered for; omitted ids are silently dropped.</summary>
    Task<IReadOnlyDictionary<int, UniversalisMarketBoardListing>> GetMarketBoardDataAsync(
        string location, IReadOnlyList<int> itemIds, CancellationToken ct = default);
}

/// <summary>StackSizeHistogram maps stack size → occurrence count.</summary>
public sealed record UniversalisMarketBoardListing(
    int MinPrice,
    double RegularSaleVelocity,
    IReadOnlyDictionary<int, int> StackSizeHistogram);
