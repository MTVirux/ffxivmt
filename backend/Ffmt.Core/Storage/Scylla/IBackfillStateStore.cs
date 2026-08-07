using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public interface IBackfillStateStore
{
    Task<IReadOnlyList<BackfillBucketState>> GetBucketsAsync(string region, string loop, CancellationToken ct = default);

    Task UpsertBucketAsync(BackfillBucketState state, CancellationToken ct = default);

    /// <summary>Pointers from the pre-bucket table, used once to seed the buckets.</summary>
    Task<(DateTimeOffset? LastImportAt, DateTimeOffset? EarliestImportAt)> GetLegacyPointersAsync(
        string region, CancellationToken ct = default);
}
