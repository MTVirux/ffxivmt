using System.Collections.Concurrent;
using Ffmt.Core.Configuration;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Mannequin;

// Counts are recomputed every pass: the HTTP history backfill keeps writing into old days.
public sealed class MannequinCapWorker(
    IMannequinSaleStore store,
    WorldStructureService worldStructure,
    IOptions<MannequinOptions> options,
    ILogger<MannequinCapWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromHours(Math.Max(1, options.Value.CleanupIntervalHours));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MannequinCapWorker: pass failed; retrying in {Hours}h", interval.TotalHours);
            }

            try { await Task.Delay(interval, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
        }
    }

    public async Task<IReadOnlyList<DateOnly>> RunOnceAsync(CancellationToken ct)
    {
        var opts = options.Value;
        var days = await store.GetDaysAsync(ct).ConfigureAwait(false);
        if (days.Count == 0)
        {
            return [];
        }

        var worldIds = (await worldStructure.GetWorldsAsync(ct).ConfigureAwait(false)).Select(w => w.Id).ToList();
        var rowsPerDay = new ConcurrentDictionary<DateOnly, long>();

        await Parallel.ForEachAsync(
            days.SelectMany(day => worldIds.Select(worldId => (Day: day, WorldId: worldId))),
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, opts.CountConcurrency), CancellationToken = ct },
            async (p, token) =>
            {
                var rows = await store.CountAsync(p.WorldId, p.Day, token).ConfigureAwait(false);
                rowsPerDay.AddOrUpdate(p.Day, rows, (_, prev) => prev + rows);
            }).ConfigureAwait(false);

        long bytesPerRow = Math.Max(1, opts.EstimatedBytesPerRow);
        var maxBytes = (long)opts.MaxSizeMb * 1024 * 1024;
        var drop = MannequinCap.DaysToDrop(rowsPerDay, bytesPerRow, maxBytes);

        foreach (var day in drop)
        {
            await store.DeleteDayAsync(worldIds, day, ct).ConfigureAwait(false);
        }

        var totalRows = rowsPerDay.Values.Sum();
        logger.LogInformation(
            "MannequinCapWorker: {Days} day(s), {Rows} row(s), ~{Mb} MB estimated, dropped {Dropped}",
            days.Count, totalRows, totalRows * bytesPerRow / (1024 * 1024),
            drop.Count == 0 ? "nothing" : string.Join(", ", drop));

        return drop;
    }
}
