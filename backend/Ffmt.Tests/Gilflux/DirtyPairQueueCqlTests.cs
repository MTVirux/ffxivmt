using Cassandra;
using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Ffmt.Tests.Gilflux;

public sealed class DirtyPairQueueCqlTests
{
    private static (ScyllaDirtyPairQueue Queue, List<string> Captured) NewQueue()
    {
        var session = Substitute.For<IScyllaSession>();
        var captured = new List<string>();
        session.PrepareAsync(Arg.Do<string>(c => captured.Add(c)), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<PreparedStatement>(null!));
        var opts = Options.Create(new GilfluxOptions());
        return (new ScyllaDirtyPairQueue(session, opts), captured);
    }

    [Fact]
    public async Task EnqueueManyAsync_PreparesInsertWithBucketEnqueuedAtItemWorld()
    {
        var (queue, captured) = NewQueue();
        try { await queue.EnqueueManyAsync(new[] { (21, 12345) }); } catch { /* no real session */ }

        captured.Should().Contain(c => c.Contains("INSERT INTO gilflux_dirty_pairs"));
        captured.Should().Contain(c => c.Contains("(bucket, enqueued_at, item_id, world_id)"));
    }

    [Fact]
    public async Task ClaimBatchAsync_PreparesSelectByBucketWithLimit()
    {
        var (queue, captured) = NewQueue();
        try { await queue.ClaimBatchAsync(100); } catch { /* no real session */ }

        captured.Should().Contain(c =>
            c.Contains("FROM gilflux_dirty_pairs") &&
            c.Contains("WHERE bucket = ?") &&
            c.Contains("LIMIT ?"));
    }

    [Fact]
    public async Task RemoveAsync_PreparesDeleteByFullPrimaryKey()
    {
        var (queue, captured) = NewQueue();
        try
        {
            await queue.RemoveAsync(new[] { new DirtyPairClaim(Guid.NewGuid(), 21, 12345) });
        }
        catch { /* no real session */ }

        captured.Should().Contain(c =>
            c.Contains("DELETE FROM gilflux_dirty_pairs") &&
            c.Contains("WHERE bucket = ? AND enqueued_at = ? AND item_id = ? AND world_id = ?"));
    }
}
