using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Quarantine;

public sealed class SaleAnomalyFilter(
    IPriceBaselineProvider baselines,
    WorldStructureService worlds,
    IOptions<QuarantineOptions> options) : ISaleAnomalyFilter
{
    public async Task<AnomalyPartition> PartitionAsync(IReadOnlyList<Sale> sales, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (!opts.Enabled || sales.Count == 0)
        {
            return new AnomalyPartition(sales, [], []);
        }

        await baselines.EnsureLoadedAsync(ct).ConfigureAwait(false);
        var worldsById = await worlds.GetWorldsByIdAsync(ct).ConfigureAwait(false);

        var accepted = new List<Sale>(sales.Count);
        var quarantined = new List<QuarantinedSale>();
        var noBaseline = new List<Sale>();
        var now = DateTimeOffset.UtcNow;

        foreach (var sale in sales)
        {
            if (!worldsById.TryGetValue(sale.WorldId, out var world)
                || !baselines.TryGet(sale.ItemId, world.Region, sale.Hq, out var baseline)
                || baseline.SampleCount < opts.MinSampleCount)
            {
                accepted.Add(sale);
                noBaseline.Add(sale);
                continue;
            }

            if (sale.UnitPrice <= opts.MinAbsoluteUnitPrice
                || sale.UnitPrice <= baseline.MedianUnitPrice * opts.MedianMultiplier)
            {
                accepted.Add(sale);
                continue;
            }

            quarantined.Add(new QuarantinedSale(
                sale, QuarantineReasons.UnitPriceDeviation, baseline.MedianUnitPrice, now));
        }

        return new AnomalyPartition(accepted, quarantined, noBaseline);
    }
}
