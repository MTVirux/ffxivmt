using System.Collections.Concurrent;

using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class BackfillChunkRunnerTests
{
    private static Task<int> Run(
        IReadOnlyList<int> chunks,
        Func<int, CancellationToken, Task<bool>> fetch,
        int retryRounds = 2,
        Func<CancellationToken, Task>? gate = null) =>
        BackfillChunkRunner.RunAsync(
            chunks, fetch, retryRounds, concurrency: 4, retryRoundDelay: TimeSpan.Zero, gate, CancellationToken.None);

    [Fact]
    public async Task Fetches_each_chunk_once_when_none_fail()
    {
        var attempts = new ConcurrentDictionary<int, int>();

        var failedChunks = await Run([1, 2, 3], (id, _) =>
        {
            attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult(true);
        });

        failedChunks.Should().Be(0);
        attempts.Keys.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        attempts.Values.Should().AllSatisfy(v => v.Should().Be(1));
    }

    [Fact]
    public async Task Retries_only_the_chunks_that_failed()
    {
        var attempts = new ConcurrentDictionary<int, int>();

        var failedChunks = await Run([1, 2, 3], (id, _) =>
        {
            var attempt = attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult(id != 2 || attempt > 1);
        });

        failedChunks.Should().Be(0,
            "the retry cleared the only failure, so the pass can advance its pointer");
        attempts[1].Should().Be(1, "chunks that already succeeded must not be refetched");
        attempts[3].Should().Be(1, "chunks that already succeeded must not be refetched");
        attempts[2].Should().Be(2, "the failed chunk is retried and then succeeds");
    }

    [Fact]
    public async Task Reports_chunks_that_still_fail_after_the_retry_rounds_are_exhausted()
    {
        var attempts = new ConcurrentDictionary<int, int>();
        var succeeded = new ConcurrentBag<int>();

        var failedChunks = await Run([1, 2, 3], (id, _) =>
        {
            attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            if (id == 2)
                return Task.FromResult(false);

            succeeded.Add(id);
            return Task.FromResult(true);
        });

        failedChunks.Should().Be(1);
        attempts[2].Should().Be(3, "the initial attempt plus two retry rounds");
        succeeded.Should().BeEquivalentTo(new[] { 1, 3 },
            "the chunks that did succeed are still handled");
    }

    [Fact]
    public async Task Rate_limit_gate_is_awaited_for_retries_too()
    {
        var gateCalls = 0;
        var attempts = new ConcurrentDictionary<int, int>();

        await Run([1, 2, 3], (id, _) =>
        {
            var attempt = attempts.AddOrUpdate(id, 1, (_, v) => v + 1);
            return Task.FromResult(id != 2 || attempt > 1);
        },
        gate: _ =>
        {
            Interlocked.Increment(ref gateCalls);
            return Task.CompletedTask;
        });

        gateCalls.Should().Be(4, "three initial requests plus the one retried request");
    }

    [Fact]
    public async Task Bounds_how_many_chunks_are_in_flight_at_once()
    {
        var inFlight = 0;
        var peak = 0;

        await BackfillChunkRunner.RunAsync(
            Enumerable.Range(1, 32).ToList(),
            async (_, token) =>
            {
                var current = Interlocked.Increment(ref inFlight);
                InterlockedMax(ref peak, current);
                await Task.Delay(5, token);
                Interlocked.Decrement(ref inFlight);
                return true;
            },
            retryRounds: 0,
            concurrency: 4,
            retryRoundDelay: TimeSpan.Zero,
            gate: null,
            CancellationToken.None);

        peak.Should().BeLessThanOrEqualTo(4, "the runner is the only concurrency bound on a pass");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref target);
            if (value <= seen)
                return;
        }
        while (Interlocked.CompareExchange(ref target, value, seen) != seen);
    }
}
