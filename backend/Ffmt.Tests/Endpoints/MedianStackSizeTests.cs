using Ffmt.Api.Endpoints;

namespace Ffmt.Tests.Endpoints;

public sealed class MedianStackSizeTests
{
    /// <summary>The implementation the cumulative-count walk replaced: flatten every observation,
    /// sort, read index n/2.</summary>
    private static int FlattenAndSort(IReadOnlyDictionary<int, int> histogram)
    {
        if (histogram.Count == 0) return 0;

        var flat = new List<int>();
        foreach (var (size, occ) in histogram)
        {
            for (var i = 0; i < occ; i++) flat.Add(size);
        }
        if (flat.Count == 0) return 0;

        flat.Sort();
        return flat[flat.Count / 2];
    }

    private static readonly Dictionary<int, int>[] Histograms =
    [
        new(),
        new() { [1] = 0 },
        new() { [5] = 7 },
        new() { [1] = 1, [2] = 1 },
        new() { [1] = 1, [2] = 1, [3] = 1 },
        new() { [99] = 3, [1] = 4, [50] = 2 },
        new() { [99] = 3, [1] = 3, [50] = 2 },
        new() { [12] = 1, [3] = 40, [7] = 40 },
        new() { [1] = 1, [2] = 5, [4] = 1, [8] = 2, [16] = 1 },
    ];

    [Fact]
    public void Matches_the_flatten_and_sort_implementation()
    {
        foreach (var histogram in Histograms)
        {
            var label = string.Join(",", histogram.Select(kv => $"{kv.Key}x{kv.Value}"));
            ToolsEndpoints.MedianStackSize(histogram).Should().Be(FlattenAndSort(histogram), "histogram was [{0}]", label);
        }
    }

    [Fact]
    public void An_empty_histogram_is_zero()
    {
        ToolsEndpoints.MedianStackSize(new Dictionary<int, int>()).Should().Be(0);
    }

    [Fact]
    public void A_single_bucket_is_its_own_median()
    {
        ToolsEndpoints.MedianStackSize(new Dictionary<int, int> { [5] = 7 }).Should().Be(5);
    }

    [Fact]
    public void An_even_total_takes_the_upper_median()
    {
        ToolsEndpoints.MedianStackSize(new Dictionary<int, int> { [1] = 1, [2] = 1 }).Should().Be(2);
    }

    [Fact]
    public void Key_order_does_not_change_the_result()
    {
        var ascending = new Dictionary<int, int> { [1] = 4, [50] = 2, [99] = 3 };
        var descending = new Dictionary<int, int> { [99] = 3, [50] = 2, [1] = 4 };

        ToolsEndpoints.MedianStackSize(descending).Should().Be(ToolsEndpoints.MedianStackSize(ascending));
    }
}
