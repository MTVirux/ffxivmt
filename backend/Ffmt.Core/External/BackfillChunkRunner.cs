using System.Collections.Concurrent;

using Ffmt.Core.Models;

namespace Ffmt.Core.External;

/// <summary>Sales gathered across every round, plus the chunks that never succeeded.</summary>
public sealed record ChunkRunResult(List<Sale> Sales, int FailedChunks);

/// <summary>
/// Runs the per-item-chunk fetches for one backfill pass and re-runs only the chunks that failed.
/// A pass that reports any failure leaves its pointer where it is, so without a retry a single
/// timed-out chunk out of thousands pins the crawl on the same window forever.
/// </summary>
public static class BackfillChunkRunner
{
    public static async Task<ChunkRunResult> RunAsync(
        IReadOnlyList<IReadOnlyList<int>> chunks,
        Func<IReadOnlyList<int>, CancellationToken, Task<List<Sale>?>> fetch,
        int retryRounds,
        int concurrency,
        TimeSpan retryRoundDelay,
        Func<CancellationToken, Task>? gate,
        CancellationToken ct)
    {
        var sales = new List<Sale>();
        var pending = chunks;

        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        for (var round = 0; ; round++)
        {
            var fetched = new ConcurrentBag<Sale>();
            var failed = new ConcurrentBag<IReadOnlyList<int>>();

            var tasks = pending.Select(async chunk =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    if (gate is not null)
                        await gate(ct);

                    var result = await fetch(chunk, ct);
                    if (result is null)
                    {
                        failed.Add(chunk);
                        return;
                    }

                    foreach (var sale in result)
                        fetched.Add(sale);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            sales.AddRange(fetched);
            pending = failed.ToList();

            if (pending.Count == 0 || round >= retryRounds)
                break;

            if (retryRoundDelay > TimeSpan.Zero)
                await Task.Delay(retryRoundDelay, ct);
        }

        return new ChunkRunResult(sales, pending.Count);
    }
}
