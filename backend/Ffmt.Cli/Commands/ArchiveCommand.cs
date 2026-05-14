using System.Collections.Concurrent;
using Ffmt.Core.Archive;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.S3;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Cli.Commands;

public sealed class ArchiveCommand(
    ISaleStore saleStore,
    IArchiveStore archiveStore,
    IS3ArchiveUploader uploader,
    IWorldStore worldStore,
    WorldStructureService worldStructure,
    IOptions<GilfluxOptions> gilflux,
    IOptions<ArchiveOptions> archiveOptions,
    ILogger<ArchiveCommand> logger)
{
    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var gilfluxOpts = gilflux.Value;
        var opts = archiveOptions.Value;
        var maxWindowMs = gilfluxOpts.TimeframesMs.Values.Max();
        var pruneThreshold = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMilliseconds(maxWindowMs)).UtcDateTime);

        var worlds = await worldStore.GetAllAsync(ct).ConfigureAwait(false);
        var itemIds = await worldStructure.GetMarketableItemIdsAsync(ct).ConfigureAwait(false);

        logger.LogInformation("Archive run: {WorldCount} worlds, {ItemCount} items, prune threshold {Threshold:d}, dry-run={DryRun}",
            worlds.Count, itemIds.Count, pruneThreshold, dryRun);

        using var semaphore = new SemaphoreSlim(opts.ExportConcurrency, opts.ExportConcurrency);

        foreach (var world in worlds)
        {
            ct.ThrowIfCancellationRequested();
            await ExportWorldAsync(world, itemIds, pruneThreshold, opts.LookbackDays, dryRun, semaphore, ct).ConfigureAwait(false);
            await HandleCorrectionsAsync(world, itemIds, pruneThreshold, opts.LookbackDays, dryRun, semaphore, ct).ConfigureAwait(false);
        }
    }

    private async Task ExportWorldAsync(
        World world, IReadOnlyList<int> itemIds, DateOnly pruneThreshold,
        int lookbackDays, bool dryRun, SemaphoreSlim semaphore, CancellationToken ct)
    {
        for (var i = 0; i < lookbackDays; i++)
        {
            var date = pruneThreshold.AddDays(-i);
            if (await archiveStore.IsExportedAsync(world.Id, date, ct).ConfigureAwait(false))
                continue;

            var sales = await CollectDaySalesAsync(world.Id, itemIds, date, semaphore, ct).ConfigureAwait(false);

            if (sales.Count == 0)
            {
                logger.LogDebug("No sales for world {World} on {Date:d} — skipping", world.Name, date);
                continue;
            }

            var key = ArchiveKey(date, world.Datacenter, world.Name);
            logger.LogInformation("{Mode} {World} {Date:d}: {Count} sales → {Key}",
                dryRun ? "DRY-RUN" : "Exporting", world.Name, date, sales.Count, key);

            if (!dryRun)
            {
                var parquet = await ArchiveParquetWriter.WriteAsync(sales).ConfigureAwait(false);
                await uploader.UploadAsync(key, parquet, ct).ConfigureAwait(false);
                await archiveStore.MarkExportedAsync(world.Id, date, ct).ConfigureAwait(false);
                await DeleteSalesAsync(world.Id, itemIds, date, sales, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task HandleCorrectionsAsync(
        World world, IReadOnlyList<int> itemIds, DateOnly pruneThreshold,
        int lookbackDays, bool dryRun, SemaphoreSlim semaphore, CancellationToken ct)
    {
        var byDate = new Dictionary<DateOnly, List<Sale>>();

        var tasks = itemIds.Select(async itemId =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                for (var i = 0; i < lookbackDays; i++)
                {
                    var date = pruneThreshold.AddDays(-i);
                    if (!await archiveStore.IsExportedAsync(world.Id, date, ct).ConfigureAwait(false))
                        continue;

                    var sales = await saleStore.GetByItemAndWorldInRangeAsync(itemId, world.Id, date, ct).ConfigureAwait(false);
                    if (sales.Count == 0) continue;

                    lock (byDate)
                    {
                        if (!byDate.TryGetValue(date, out var list))
                            byDate[date] = list = [];
                        list.AddRange(sales);
                    }
                }
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var (date, newRows) in byDate)
        {
            var key = CorrectionsKey(date, world.Datacenter, world.Name);
            logger.LogInformation("{Mode} corrections for {World} {Date:d}: {Count} late rows → {Key}",
                dryRun ? "DRY-RUN" : "Writing", world.Name, date, newRows.Count, key);

            if (!dryRun)
            {
                var existing = await uploader.DownloadAsync(key, ct).ConfigureAwait(false);
                var merged = existing is not null
                    ? MergeAndDeduplicate(await ArchiveParquetWriter.ReadAsync(existing).ConfigureAwait(false), newRows)
                    : newRows;

                var parquet = await ArchiveParquetWriter.WriteAsync(merged).ConfigureAwait(false);
                await uploader.UploadAsync(key, parquet, ct).ConfigureAwait(false);
                await DeleteSalesAsync(world.Id, itemIds, date, newRows, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task<List<Sale>> CollectDaySalesAsync(
        int worldId, IReadOnlyList<int> itemIds, DateOnly date,
        SemaphoreSlim semaphore, CancellationToken ct)
    {
        var bag = new ConcurrentBag<Sale>();

        var tasks = itemIds.Select(async itemId =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var sales = await saleStore.GetByItemAndWorldInRangeAsync(itemId, worldId, date, ct).ConfigureAwait(false);
                foreach (var s in sales) bag.Add(s);
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return bag.ToList();
    }

    private async Task DeleteSalesAsync(
        int worldId, IReadOnlyList<int> itemIds, DateOnly date,
        IReadOnlyList<Sale> sales, CancellationToken ct)
    {
        var bySalesItem = sales.GroupBy(s => s.ItemId).ToDictionary(g => g.Key, g => (IReadOnlyList<Sale>)g.ToList());

        foreach (var itemId in itemIds)
        {
            if (!bySalesItem.TryGetValue(itemId, out var itemSales)) continue;
            await saleStore.DeleteByItemAndWorldInRangeAsync(itemId, worldId, date, itemSales, ct).ConfigureAwait(false);
        }
    }

    private static List<Sale> MergeAndDeduplicate(IReadOnlyList<Sale> existing, IReadOnlyList<Sale> incoming)
    {
        var seen = new HashSet<(int ItemId, int WorldId, DateTimeOffset SaleTime)>(
            existing.Select(s => (s.ItemId, s.WorldId, s.SaleTime)));
        var result = new List<Sale>(existing);
        foreach (var s in incoming)
        {
            if (seen.Add((s.ItemId, s.WorldId, s.SaleTime)))
                result.Add(s);
        }
        return result;
    }

    private static string ArchiveKey(DateOnly date, string dc, string world) =>
        $"archive/{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";

    private static string CorrectionsKey(DateOnly date, string dc, string world) =>
        $"corrections/{date.Year}/{date.Month:D2}/{date.Day:D2}/{dc}/{world}.parquet";
}
