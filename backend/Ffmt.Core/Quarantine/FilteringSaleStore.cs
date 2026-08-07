using System.Globalization;
using Ffmt.Core.Configuration;
using Ffmt.Core.Metrics;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Quarantine;

/// <summary>
/// Single chokepoint for anomaly filtering. Both ingest paths call ISaleWriter.AddBatchAsync and are
/// protected without knowing this exists; every failure mode here writes the sale rather than losing it.
/// </summary>
public sealed class FilteringSaleStore(
    ISaleWriter inner,
    ISaleAnomalyFilter filter,
    IQuarantineStore quarantine,
    IOptions<QuarantineOptions> options,
    ILogger<FilteringSaleStore> logger) : ISaleWriter
{
    public async Task<SaleBatchResult> AddBatchAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.Enabled || sales.Count == 0)
        {
            return await inner.AddBatchAsync(sales, ct).ConfigureAwait(false);
        }

        var partition = await filter.PartitionAsync(sales, ct).ConfigureAwait(false);

        foreach (var group in partition.NoBaseline.GroupBy(s => s.WorldId))
        {
            MetricsCatalog.SalesNoBaselineTotal
                .WithLabels(group.Key.ToString(CultureInfo.InvariantCulture))
                .Inc(group.Count());
        }

        if (partition.Quarantined.Count > 0)
        {
            try
            {
                await quarantine.AddBatchAsync(partition.Quarantined, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Quarantine write failed for {Count} sale(s); they are being kept in sales.",
                    partition.Quarantined.Count);
            }

            foreach (var group in partition.Quarantined.GroupBy(q => (q.Sale.WorldId, q.Reason)))
            {
                MetricsCatalog.SalesQuarantinedTotal
                    .WithLabels(group.Key.WorldId.ToString(CultureInfo.InvariantCulture), group.Key.Reason)
                    .Inc(group.Count());
            }
        }

        var toWrite = opts.ShadowMode ? sales : partition.Accepted;
        return await inner.AddBatchAsync(toWrite, ct).ConfigureAwait(false);
    }
}
