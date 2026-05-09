namespace Ffmt.Core.Gilflux;

public sealed record DirtyPairClaim(Guid EnqueuedAt, int WorldId, int ItemId);

public interface IDirtyPairQueue
{
    Task EnqueueManyAsync(IReadOnlyCollection<(int WorldId, int ItemId)> pairs, CancellationToken ct = default);

    Task<IReadOnlyList<DirtyPairClaim>> ClaimBatchAsync(int batchSize, CancellationToken ct = default);

    Task RemoveAsync(IReadOnlyCollection<DirtyPairClaim> claims, CancellationToken ct = default);
}
