using System.Net;
using System.Text.Json;
using Ffmt.Core.External;
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
}
