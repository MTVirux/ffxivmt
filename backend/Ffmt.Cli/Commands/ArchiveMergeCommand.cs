using Ffmt.Core.Archive;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.S3;
using Microsoft.Extensions.Logging;

namespace Ffmt.Cli.Commands;

public sealed class ArchiveMergeCommand(
    IS3ArchiveUploader uploader,
    ILogger<ArchiveMergeCommand> logger)
{
    public async Task RunAsync(bool dryRun, CancellationToken ct)
    {
        var correctionKeys = await uploader.ListKeysAsync("corrections/", ct).ConfigureAwait(false);

        logger.LogInformation("Merge run: {Count} corrections files to process, dry-run={DryRun}",
            correctionKeys.Count, dryRun);

        foreach (var corrKey in correctionKeys)
        {
            ct.ThrowIfCancellationRequested();

            var archiveKey = "archive/" + corrKey["corrections/".Length..];

            logger.LogInformation("{Mode} merging {CorrKey} into {ArchiveKey}",
                dryRun ? "DRY-RUN" : "Merging", corrKey, archiveKey);

            if (dryRun) continue;

            var corrBytes = await uploader.DownloadAsync(corrKey, ct).ConfigureAwait(false);
            if (corrBytes is null)
            {
                logger.LogWarning("Corrections file {Key} vanished during merge — skipping", corrKey);
                continue;
            }

            var corrSales = await ArchiveParquetWriter.ReadAsync(corrBytes).ConfigureAwait(false);

            var archiveBytes = await uploader.DownloadAsync(archiveKey, ct).ConfigureAwait(false);
            var archiveSales = archiveBytes is not null
                ? await ArchiveParquetWriter.ReadAsync(archiveBytes).ConfigureAwait(false)
                : new List<Sale>();

            var merged = MergeAndDeduplicate(archiveSales, corrSales);
            var mergedParquet = await ArchiveParquetWriter.WriteAsync(merged).ConfigureAwait(false);

            await uploader.UploadAsync(archiveKey, mergedParquet, ct).ConfigureAwait(false);
            await uploader.DeleteAsync(corrKey, ct).ConfigureAwait(false);

            logger.LogInformation("Merged {Count} rows into {ArchiveKey}, deleted {CorrKey}",
                merged.Count, archiveKey, corrKey);
        }
    }

    private static List<Sale> MergeAndDeduplicate(IReadOnlyList<Sale> existing, IReadOnlyList<Sale> incoming)
    {
        var seen = new HashSet<(int, int, DateTimeOffset)>(
            existing.Select(s => (s.ItemId, s.WorldId, s.SaleTime)));
        var result = new List<Sale>(existing);
        foreach (var s in incoming)
        {
            if (seen.Add((s.ItemId, s.WorldId, s.SaleTime)))
                result.Add(s);
        }
        return result;
    }
}
