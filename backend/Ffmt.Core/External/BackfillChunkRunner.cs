using System.Collections.Concurrent;

namespace Ffmt.Core.External;

/// <summary>
/// Runs the per-chunk fetches for one backfill pass and re-runs only the chunks that failed.
/// A pass that reports any failure leaves its pointer where it is, so without a retry a single
/// timed-out chunk out of thousands pins the crawl on the same window forever.
/// </summary>
public static class BackfillChunkRunner
{
    /// <summary>
    /// <paramref name="fetch"/> returns false for a chunk that should be retried, and owns whatever
    /// it does with a successful result. Returns the number of chunks that never succeeded.
    /// </summary>
    public static async Task<int> RunAsync<TChunk>(
        IReadOnlyList<TChunk> chunks,
        Func<TChunk, CancellationToken, Task<bool>> fetch,
        int retryRounds,
        int concurrency,
        TimeSpan retryRoundDelay,
        Func<CancellationToken, Task>? gate,
        CancellationToken ct)
    {
        var pending = chunks;

        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        for (var round = 0; ; round++)
        {
            var failed = new ConcurrentBag<TChunk>();

            var tasks = pending.Select(async chunk =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    if (gate is not null)
                        await gate(ct);

                    if (!await fetch(chunk, ct))
                        failed.Add(chunk);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            pending = failed.ToList();

            if (pending.Count == 0 || round >= retryRounds)
                return pending.Count;

            if (retryRoundDelay > TimeSpan.Zero)
                await Task.Delay(retryRoundDelay, ct);
        }
    }
}
