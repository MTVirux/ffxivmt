using Ffmt.Core.Metrics;
using Prometheus;

namespace Ffmt.Tests.Metrics;

public sealed class MetricsCatalogTests
{
    [Fact]
    public void All_instruments_are_registered_and_non_null()
    {
        MetricsCatalog.All.Should().HaveCount(28, "spec calls out 28 named instruments");
        MetricsCatalog.All.Should().AllSatisfy(c => c.Should().NotBeNull());
    }

    [Fact]
    public void Instrument_names_have_ffmt_prefix_and_known_suffixes()
    {
        var allowedSuffixes = new[] { "_total", "_seconds", "_inflight", "_connected", "_depth", "_busy", "_state", "_rows", "_buckets" };
        foreach (var collector in MetricsCatalog.All)
        {
            collector.Name.Should().StartWith("ffmt_", "all FFMT instruments use the ffmt_ prefix");
            allowedSuffixes
                .Any(s => collector.Name.EndsWith(s))
                .Should().BeTrue($"{collector.Name} must use a Prometheus-convention suffix");
        }
    }

    [Fact]
    public void Backfill_instruments_are_labelled_by_region_not_world()
    {
        var backfill = new Collector[]
        {
            MetricsCatalog.BackfillPagesTotal,
            MetricsCatalog.BackfillRowsTotal,
            MetricsCatalog.BackfillState,
        };

        foreach (var collector in backfill)
        {
            collector.LabelNames.Should().Contain("region",
                $"{collector.Name} is emitted per import region, not per world");
            collector.LabelNames.Should().NotContain("world",
                $"{collector.Name} labels a region value, so calling it world misreads the dashboard");
        }
    }

    [Fact]
    public void Backfill_counts_passes_that_refused_to_advance_their_pointer()
    {
        // A pass that leaves its pointer put re-crawls the same window forever, and nothing else
        // distinguishes that from healthy work.
        MetricsCatalog.BackfillPointerStalledTotal.LabelNames.Should().Contain("region");
        MetricsCatalog.BackfillPointerStalledTotal.LabelNames.Should().Contain("loop");
    }

    [Fact]
    public void Backfill_state_is_tracked_per_loop()
    {
        MetricsCatalog.BackfillState.LabelNames.Should().Contain("loop",
            "the live-gap and historical-crawl loops run concurrently and would otherwise overwrite each other");
    }

    [Fact]
    public void Forbidden_high_cardinality_labels_are_not_used()
    {
        var forbidden = new[] { "item_id", "item", "sale_id" };
        foreach (var collector in MetricsCatalog.All)
        {
            foreach (var label in collector.LabelNames)
            {
                forbidden.Should().NotContain(label,
                    $"{collector.Name} has forbidden label {label} (item_id/sale_id explode cardinality)");
            }
        }
    }
}
