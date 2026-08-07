using Cassandra;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using NSubstitute;

namespace Ffmt.Tests.Storage.Scylla;

public sealed class BackfillStateStoreCqlTests
{
    private static (ScyllaBackfillStateStore Store, List<string> Captured) NewStore()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        return (new ScyllaBackfillStateStore(session), captured);
    }

    [Fact]
    public async Task GetBucketsAsync_reads_a_single_partition()
    {
        var (store, captured) = NewStore();

        try { await store.GetBucketsAsync("Europe", "historical"); } catch { }

        captured.Should().Contain(c =>
            c.Contains("FROM ffmt.backfill_bucket_state") &&
            c.Contains("region = ?") &&
            c.Contains("loop = ?"));
        captured.Should().NotContain(c => c.Contains("ALLOW FILTERING"),
            "a pass reads every bucket for a region on each cycle - it must stay one partition read");
    }

    [Fact]
    public async Task UpsertBucketAsync_targets_the_full_primary_key()
    {
        var (store, captured) = NewStore();

        try
        {
            await store.UpsertBucketAsync(new BackfillBucketState(
                "Europe", "historical", 12, null, DateTimeOffset.UnixEpoch, CrawlComplete: false));
        }
        catch { }

        captured.Should().Contain(c =>
            c.Contains("INSERT INTO ffmt.backfill_bucket_state") &&
            c.Contains("region") && c.Contains("loop") && c.Contains("bucket"));
    }

    [Fact]
    public async Task GetLegacyPointersAsync_reads_the_pre_bucket_table()
    {
        var (store, captured) = NewStore();

        try { await store.GetLegacyPointersAsync("Europe"); } catch { }

        captured.Should().Contain(c =>
            c.Contains("FROM ffmt.backfill_state") &&
            c.Contains("region = ?"),
            "seeding preserves the history already imported under the single-pointer scheme");
        captured.Should().NotContain(c => c.Contains("backfill_bucket_state"));
    }
}
