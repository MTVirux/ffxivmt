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

    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var ranges = TokenRanges.Split(RangeCount);
        long found = 0;
        var done = 0;

        logger.LogInformation("{Mode} mannequin backfill over {Ranges} token ranges",
            dryRun ? "DRY-RUN" : "Running", ranges.Count);

        await Parallel.ForEachAsync(
            ranges,
            new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = ct },
            async (range, token) =>
            {
                var sales = await saleStore.GetMannequinInTokenRangeAsync(range.Start, range.End, token).ConfigureAwait(false);
                if (!dryRun && sales.Count > 0)
                {
                    await mannequinStore.AddAsync(sales, token).ConfigureAwait(false);
                }

                Interlocked.Add(ref found, sales.Count);
                var n = Interlocked.Increment(ref done);
                if (n % LogEvery == 0)
                {
                    logger.LogInformation("Mannequin backfill: {Done}/{Total} ranges, {Found} sale(s)",
                        n, ranges.Count, Interlocked.Read(ref found));
                }
            }).ConfigureAwait(false);

        logger.LogInformation("{Mode} mannequin backfill done: {Found} sale(s) {Verb}",
            dryRun ? "DRY-RUN" : "Running", found, dryRun ? "found" : "copied");
    }
}
