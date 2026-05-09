namespace Ffmt.Core.Gilflux;

public interface IRankingRefresher
{
    Task RefreshAsync(int worldId, int itemId, CancellationToken ct = default);

    Task RefreshManyAsync(IReadOnlyCollection<(int WorldId, int ItemId)> pairs, int maxConcurrency, CancellationToken ct = default);
}
