using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Stages;

public static class ProgressLoop
{
    public const int DefaultConcurrency = 16;

    private const int ProgressEvery = 1000;

    public static async Task RunAsync<T>(
        IReadOnlyCollection<T> rows,
        ILogger log,
        string label,
        Func<T, CancellationToken, Task> action,
        int concurrency,
        CancellationToken ct)
    {
        var total = rows.Count;
        var done = 0;

        using var semaphore = new SemaphoreSlim(concurrency, concurrency);

        var tasks = rows.Select(async row =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                await action(row, ct).ConfigureAwait(false);

                var completed = Interlocked.Increment(ref done);
                if (completed % ProgressEvery == 0)
                {
                    log.LogInformation("{Label}: {Done}/{Total}.", label, completed, total);
                }
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        log.LogInformation("{Label}: {Total} total.", label, total);
    }
}
