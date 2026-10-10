using System.Collections.Concurrent;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Commands;

public sealed class BackfillMannequinCommand(
    ISaleStore saleStore,
    IMannequinSaleStore mannequinStore,
    ILogger<BackfillMannequinCommand> logger)
{
    public const int RangeCount = 4096;
    private const int Concurrency = 4;
    private const int LogEvery = 256;
    private const int MaxAttempts = 3;

    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(2);

    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var ranges = TokenRanges.Split(RangeCount);
        var failed = new ConcurrentBag<(long Start, long End)>();
        long found = 0;
        var done = 0;

        logger.LogInformation("{Mode} mannequin backfill over {Ranges} token ranges",
            dryRun ? "DRY-RUN" : "Running", ranges.Count);

        await Parallel.ForEachAsync(
            ranges,
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = ct },
            async (range, token) =>
            {
                var count = await CopyRangeAsync(range, dryRun, token).ConfigureAwait(false);
                if (count is null)
                {
                    failed.Add(range);
                }
                else
                {
                    Interlocked.Add(ref found, count.Value);
                }

                var n = Interlocked.Increment(ref done);
                if (n % LogEvery == 0)
                {
                    logger.LogInformation("Mannequin backfill: {Done}/{Total} ranges, {Found} sale(s)",
                        n, ranges.Count, Interlocked.Read(ref found));
                }
            }).ConfigureAwait(false);

        logger.LogInformation("{Mode} mannequin backfill done: {Found} sale(s) {Verb}",
            dryRun ? "DRY-RUN" : "Running", found, dryRun ? "found" : "copied");

        if (!failed.IsEmpty)
        {
            var failedRanges = string.Join(", ", failed.OrderBy(r => r.Start).Select(r => $"({r.Start}, {r.End}]"));
            logger.LogError("Mannequin backfill: {Count} range(s) failed after {Attempts} attempts - re-run to retry them: {Ranges}",
                failed.Count, MaxAttempts, failedRanges);
            throw new InvalidOperationException($"{failed.Count} token range(s) failed; re-run backfill-mannequin to retry them.");
        }
    }

    // Returns the number of mannequin sales in the range, or null once every attempt has failed.
    private async Task<int?> CopyRangeAsync((long Start, long End) range, bool dryRun, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var sales = await saleStore.GetMannequinInTokenRangeAsync(range.Start, range.End, ct).ConfigureAwait(false);
                if (!dryRun && sales.Count > 0)
                {
                    await mannequinStore.AddAsync(sales, ct).ConfigureAwait(false);
                }
                return sales.Count;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (attempt == MaxAttempts)
                {
                    logger.LogWarning(ex, "Mannequin backfill: range ({Start}, {End}] failed after {Attempts} attempts",
                        range.Start, range.End, attempt);
                    return null;
                }

                await Task.Delay(RetryDelay * attempt, ct).ConfigureAwait(false);
            }
        }
    }
}
