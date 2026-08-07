using Ffmt.Core.Models;

namespace Ffmt.Core.Storage.Scylla;

public sealed class ScyllaBackfillStateStore(IScyllaSession scylla) : IBackfillStateStore
{
    private const string SelectBucketsCql =
        "SELECT bucket, last_import_at, earliest_import_at, crawl_complete " +
        "FROM ffmt.backfill_bucket_state WHERE region = ? AND loop = ?";

    private const string UpsertBucketCql =
        "INSERT INTO ffmt.backfill_bucket_state " +
        "(region, loop, bucket, last_import_at, earliest_import_at, crawl_complete) " +
        "VALUES (?, ?, ?, ?, ?, ?)";

    private const string SelectLegacyCql =
        "SELECT last_import_at, earliest_import_at FROM ffmt.backfill_state WHERE region = ?";

    public async Task<IReadOnlyList<BackfillBucketState>> GetBucketsAsync(
        string region, string loop, CancellationToken ct = default)
    {
        var prepared = await scylla.PrepareAsync(SelectBucketsCql, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(prepared.Bind(region, loop)).ConfigureAwait(false);

        var states = new List<BackfillBucketState>();
        foreach (var row in rows)
        {
            states.Add(new BackfillBucketState(
                region,
                loop,
                row.GetValue<int>("bucket"),
                row.SafeTimestamp("last_import_at"),
                row.SafeTimestamp("earliest_import_at"),
                row.SafeBool("crawl_complete")));
        }

        return states;
    }

    public async Task UpsertBucketAsync(BackfillBucketState state, CancellationToken ct = default)
    {
        var prepared = await scylla.PrepareAsync(UpsertBucketCql, ct).ConfigureAwait(false);
        await scylla.Session.ExecuteAsync(prepared.Bind(
            state.Region,
            state.Loop,
            state.Bucket,
            state.LastImportAt,
            state.EarliestImportAt,
            state.CrawlComplete)).ConfigureAwait(false);
    }

    public async Task<(DateTimeOffset? LastImportAt, DateTimeOffset? EarliestImportAt)> GetLegacyPointersAsync(
        string region, CancellationToken ct = default)
    {
        var prepared = await scylla.PrepareAsync(SelectLegacyCql, ct).ConfigureAwait(false);
        var rows = await scylla.Session.ExecuteAsync(prepared.Bind(region)).ConfigureAwait(false);

        var row = rows.FirstOrDefault();
        if (row is null)
        {
            return (null, null);
        }

        return (row.SafeTimestamp("last_import_at"), row.SafeTimestamp("earliest_import_at"));
    }
}
