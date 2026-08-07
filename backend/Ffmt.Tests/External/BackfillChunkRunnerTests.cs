using System.Collections.Concurrent;

using Ffmt.Core.External;
using Ffmt.Core.Models;

namespace Ffmt.Tests.External;

public sealed class BackfillChunkRunnerTests
{
    private static Sale SaleFor(int itemId) =>
        new(itemId, 40, "Buyer", false, false, 1, 100, DateTimeOffset.UnixEpoch);

    private static IReadOnlyList<IReadOnlyList<int>> Chunks(params int[][] chunks) =>
        chunks.Select(c => (IReadOnlyList<int>)c).ToList();

    private static Task<ChunkRunResult> Run(
        IReadOnlyList<IReadOnlyList<int>> chunks,
        Func<IReadOnlyList<int>, CancellationToken, Task<List<Sale>?>> fetch,
        int retryRounds = 2,
        Func<CancellationToken, Task>? gate = null) =>
        BackfillChunkRunner.RunAsync(
            chunks, fetch, retryRounds, concurrency: 4, retryRoundDelay: TimeSpan.Zero, gate, CancellationToken.None);

    [Fact]
    public async Task Fetches_each_chunk_once_when_none_fail()
    {
        var attempts = new ConcurrentDictionary<int, int>();

        var result = await Run(Chunks([1], [2], [3]), (items, _) =>
        {
            attempts.AddOrUpdate(items[0], 1, (_, v) => v + 1);
            return Task.FromResult<List<Sale>?>([SaleFor(items[0])]);
        });

        result.FailedChunks.Should().Be(0);
        result.Sales.Select(s => s.ItemId).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        attempts.Values.Should().AllSatisfy(v => v.Should().Be(1));
    }

    [Fact]
    public async Task Retries_only_the_chunks_that_failed()
    {
        var attempts = new ConcurrentDictionary<int, int>();

        var result = await Run(Chunks([1], [2], [3]), (items, _) =>
        {
            var id = items[0];
            var attempt = attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult<List<Sale>?>(id == 2 && attempt == 1 ? null : [SaleFor(id)]);
        });

        result.FailedChunks.Should().Be(0,
            "the retry cleared the only failure, so the pass can advance its pointer");
        result.Sales.Select(s => s.ItemId).Should().BeEquivalentTo(new[] { 1, 2, 3 });
        attempts[1].Should().Be(1, "chunks that already succeeded must not be refetched");
        attempts[3].Should().Be(1, "chunks that already succeeded must not be refetched");
        attempts[2].Should().Be(2, "the failed chunk is retried and then succeeds");
    }

    [Fact]
    public async Task Reports_chunks_that_still_fail_after_the_retry_rounds_are_exhausted()
    {
        var attempts = new ConcurrentDictionary<int, int>();

        var result = await Run(Chunks([1], [2], [3]), (items, _) =>
        {
            var id = items[0];
            attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult<List<Sale>?>(id == 2 ? null : [SaleFor(id)]);
        });

        result.FailedChunks.Should().Be(1);
        attempts[2].Should().Be(3, "the initial attempt plus two retry rounds");
        result.Sales.Select(s => s.ItemId).Should().BeEquivalentTo(new[] { 1, 3 },
            "sales from the chunks that did succeed are still returned to the caller");
    }

    [Fact]
    public async Task Rate_limit_gate_is_awaited_for_retries_too()
    {
        var gateCalls = 0;
        var attempts = new ConcurrentDictionary<int, int>();

        await Run(Chunks([1], [2], [3]), (items, _) =>
        {
            var id = items[0];
            var attempt = attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult<List<Sale>?>(id == 2 && attempt == 1 ? null : [SaleFor(id)]);
        },
        gate: _ =>
        {
            Interlocked.Increment(ref gateCalls);
            return Task.CompletedTask;
        });

        gateCalls.Should().Be(4, "three initial requests plus the one retried request");
    }
}
