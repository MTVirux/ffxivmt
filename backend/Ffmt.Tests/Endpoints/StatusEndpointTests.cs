using System.Net;
using System.Text.Json;
using Ffmt.Core.External;
using Ffmt.Core.Models;
using Ffmt.Core.Status;
using Ffmt.Tests.Fakes;
using NSubstitute;

namespace Ffmt.Tests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class StatusEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task GET_status_metrics_returns_the_snake_case_envelope()
    {
        factory.Prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(1.0);
        factory.Prometheus.QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>()).Returns(80.0);
        factory.Prometheus.QueryRangeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([new SeriesPoint(1760000000, 1.5)]);

        var resp = await factory.CreateClient().GetAsync("/api/v1/status/metrics");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().Equal("status", "message", "data");
        root.GetProperty("status").GetBoolean().Should().BeTrue();
        root.GetProperty("message").GetString().Should().Be("Status metrics retrieved successfully");

        var data = root.GetProperty("data");
        data.GetProperty("available").GetBoolean().Should().BeTrue();
        data.GetProperty("generated_at").GetInt64().Should().BePositive();

        var metrics = data.GetProperty("metrics").EnumerateObject().ToList();
        metrics.Where(m => m.Value.TryGetProperty("total", out _)).Select(m => m.Name).Should().Equal("worlds_connected");
        metrics.Single(m => m.Name == "worlds_connected").Value.GetProperty("total").GetDouble().Should().Be(80);
    }

    [Fact]
    public async Task GET_status_backfill_returns_per_region_progress()
    {
        var reached = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var slowest = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var advanced = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
        factory.BackfillState.GetBucketProgressAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<BackfillBucketProgress>());
        factory.BackfillState.GetBucketProgressAsync("Europe", BackfillLoops.Historical, Arg.Any<CancellationToken>())
            .Returns(
            [
                new BackfillBucketProgress(0, reached, CrawlComplete: true, PointerWrittenAt: null),
                new BackfillBucketProgress(1, slowest, CrawlComplete: false, PointerWrittenAt: advanced),
            ]);

        var resp = await factory.CreateClient().GetAsync("/api/v1/status/backfill");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        root.EnumerateObject().Select(p => p.Name).Should().Equal("status", "message", "data");
        root.GetProperty("message").GetString().Should().Be("Backfill progress retrieved successfully");

        var data = root.GetProperty("data");
        data.EnumerateObject().Select(p => p.Name).Should().Equal("available", "generated_at", "regions");
        data.GetProperty("available").GetBoolean().Should().BeTrue();
        data.GetProperty("generated_at").GetInt64().Should().BePositive();

        var regions = data.GetProperty("regions").EnumerateArray().ToList();
        regions.Select(r => r.GetProperty("region").GetString())
            .Should().Equal("Europe", "North-America", "Japan", "Oceania");

        var europe = regions[0];
        europe.EnumerateObject().Select(p => p.Name).Should().Equal(
            "region", "buckets_total", "buckets_complete", "reached_back_to", "slowest_bucket_at", "last_advanced_at");
        europe.GetProperty("buckets_total").GetInt32().Should().Be(2);
        europe.GetProperty("buckets_complete").GetInt32().Should().Be(1);
        europe.GetProperty("reached_back_to").GetInt64().Should().Be(reached.ToUnixTimeSeconds());
        europe.GetProperty("slowest_bucket_at").GetInt64().Should().Be(slowest.ToUnixTimeSeconds());
        europe.GetProperty("last_advanced_at").GetInt64().Should().Be(advanced.ToUnixTimeSeconds());

        var japan = regions[2];
        japan.GetProperty("buckets_total").GetInt32().Should().Be(0);
        japan.GetProperty("reached_back_to").ValueKind.Should().Be(JsonValueKind.Null);
        japan.GetProperty("slowest_bucket_at").ValueKind.Should().Be(JsonValueKind.Null);
        japan.GetProperty("last_advanced_at").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
