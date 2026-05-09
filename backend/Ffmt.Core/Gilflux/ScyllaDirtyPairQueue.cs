using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Gilflux;

public sealed class ScyllaDirtyPairQueue(IScyllaSession scylla, IOptions<GilfluxOptions> options) : IDirtyPairQueue
{
    private readonly int _bucket = options.Value.DirtyPairBucket;

    private const string CqlInsert = """
        INSERT INTO gilflux_dirty_pairs
            (bucket, enqueued_at, item_id, world_id)
        VALUES (?, now(), ?, ?)
        """;

    private const string CqlClaim = """
        SELECT enqueued_at, item_id, world_id
        FROM gilflux_dirty_pairs
        WHERE bucket = ?
        LIMIT ?
        """;

    private const string CqlDelete = """
        DELETE FROM gilflux_dirty_pairs
        WHERE bucket = ? AND enqueued_at = ? AND item_id = ? AND world_id = ?
        """;

    public async Task EnqueueManyAsync(IReadOnlyCollection<(int WorldId, int ItemId)> pairs, CancellationToken ct = default)
    {
        if (pairs.Count == 0)
        {
            return;
        }

        var stmt = await scylla.PrepareAsync(CqlInsert, ct).ConfigureAwait(false);

        const int rowsPerBatch = 200;
        for (var i = 0; i < pairs.Count; i += rowsPerBatch)
        {
            var slice = pairs.Skip(i).Take(rowsPerBatch).ToList();
            var batch = (BatchStatement)new BatchStatement()
                .SetBatchType(BatchType.Unlogged)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne);
            foreach (var (worldId, itemId) in slice)
            {
                batch.Add(stmt.Bind(_bucket, itemId, worldId));
            }
            await scylla.Session.ExecuteAsync(batch).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<DirtyPairClaim>> ClaimBatchAsync(int batchSize, CancellationToken ct = default)
    {
        if (batchSize <= 0)
        {
            return Array.Empty<DirtyPairClaim>();
        }

        var stmt = await scylla.PrepareAsync(CqlClaim, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(stmt.Bind(_bucket, batchSize)).ConfigureAwait(false);

        var result = new List<DirtyPairClaim>();
        foreach (var row in rows)
        {
            result.Add(new DirtyPairClaim(
                EnqueuedAt: row.GetValue<Guid>("enqueued_at"),
                WorldId:    row.GetValue<int>("world_id"),
                ItemId:     row.GetValue<int>("item_id")));
        }
        return result;
    }

    public async Task RemoveAsync(IReadOnlyCollection<DirtyPairClaim> claims, CancellationToken ct = default)
    {
        if (claims.Count == 0)
        {
            return;
        }

        var stmt = await scylla.PrepareAsync(CqlDelete, ct).ConfigureAwait(false);

        const int rowsPerBatch = 200;
        var asList = claims as IReadOnlyList<DirtyPairClaim> ?? claims.ToList();
        for (var i = 0; i < asList.Count; i += rowsPerBatch)
        {
            var slice = asList.Skip(i).Take(rowsPerBatch).ToList();
            var batch = (BatchStatement)new BatchStatement()
                .SetBatchType(BatchType.Unlogged)
                .SetConsistencyLevel(ConsistencyLevel.LocalOne);
            foreach (var c in slice)
            {
                batch.Add(stmt.Bind(_bucket, c.EnqueuedAt, c.ItemId, c.WorldId));
            }
            await scylla.Session.ExecuteAsync(batch).ConfigureAwait(false);
        }
    }
}
