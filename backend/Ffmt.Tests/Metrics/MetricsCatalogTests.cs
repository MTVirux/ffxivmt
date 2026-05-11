using Ffmt.Core.Metrics;
using Prometheus;

namespace Ffmt.Tests.Metrics;

public sealed class MetricsCatalogTests
{
    [Fact]
    public void All_instruments_are_registered_and_non_null()
    {
        MetricsCatalog.All.Should().HaveCount(17, "spec calls out 17 named instruments");
        MetricsCatalog.All.Should().AllSatisfy(c => c.Should().NotBeNull());
    }

    [Fact]
    public void Instrument_names_have_ffmt_prefix_and_known_suffixes()
    {
        var allowedSuffixes = new[] { "_total", "_seconds", "_inflight", "_connected", "_depth", "_busy", "_state" };
        foreach (var collector in MetricsCatalog.All)
        {
            collector.Name.Should().StartWith("ffmt_", "all FFMT instruments use the ffmt_ prefix");
            allowedSuffixes
                .Any(s => collector.Name.EndsWith(s))
                .Should().BeTrue($"{collector.Name} must use a Prometheus-convention suffix");
        }
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
