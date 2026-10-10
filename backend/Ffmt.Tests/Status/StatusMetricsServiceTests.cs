using System.Text.Json;
using Ffmt.Core.External;
using Ffmt.Core.Status;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Ffmt.Tests.Status;

public sealed class StatusMetricsServiceTests : IDisposable
{
    private readonly IPrometheusClient _prometheus = Substitute.For<IPrometheusClient>();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public void Dispose() => _cache.Dispose();

    private StatusMetricsService CreateService() =>
        new(_prometheus, _cache, TimeProvider.System, NullLogger<StatusMetricsService>.Instance);

    private void PrometheusReturns(double? value, double? worldsTotal = 80, double? salesLast10m = 1000)
    {
        _prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(value);
        _prometheus.QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>()).Returns(worldsTotal);
        _prometheus.QueryAsync(StatusMetricsService.SalesLast10mQuery, Arg.Any<CancellationToken>()).Returns(salesLast10m);
        _prometheus.QueryRangeAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns([new SeriesPoint(1760000000, 1.5)]);
    }

    [Fact]
    public async Task Returns_every_metric_with_series()
    {
        PrometheusReturns(80);

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.Available.Should().BeTrue();
        response.Metrics.Keys.Should().BeEquivalentTo(
            "requests_per_second", "error_rate", "sales_per_second", "worlds_connected", "backfill_rows_per_second");
        response.Metrics["worlds_connected"].Total.Should().Be(80);
        response.Metrics["requests_per_second"].Total.Should().BeNull();
        response.Metrics["sales_per_second"].Series.Should().Equal(new SeriesPoint(1760000000, 1.5));
    }

    [Fact]
    public async Task Evaluates_the_banner()
    {
        PrometheusReturns(80, salesLast10m: 0);

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.State.Should().Be("down");
    }

    [Fact]
    public async Task Prometheus_failure_reports_unavailable()
    {
        _prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("refused"));

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.Available.Should().BeFalse();
        response.State.Should().Be("unknown");
        response.Metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task Prometheus_timeout_reports_unavailable()
    {
        _prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TaskCanceledException("timeout"));

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.Available.Should().BeFalse();
    }

    [Fact]
    public async Task Caches_the_response()
    {
        PrometheusReturns(80);
        var service = CreateService();

        await service.GetAsync(CancellationToken.None);
        await service.GetAsync(CancellationToken.None);

        await _prometheus.Received(1).QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_request_is_not_cached_as_unavailable()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        _prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TaskCanceledException());

        await CreateService().Invoking(s => s.GetAsync(cts.Token)).Should().ThrowAsync<OperationCanceledException>();

        PrometheusReturns(80);
        (await CreateService().GetAsync(CancellationToken.None)).Available.Should().BeTrue();
    }

    [Fact]
    public async Task Serializes_to_the_documented_wire_shape()
    {
        PrometheusReturns(80);
        var response = await CreateService().GetAsync(CancellationToken.None);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        using var doc = JsonDocument.Parse(json);
        var metrics = doc.RootElement.GetProperty("metrics");

        doc.RootElement.TryGetProperty("generated_at", out _).Should().BeTrue();
        metrics.GetProperty("worlds_connected").GetProperty("total").GetDouble().Should().Be(80);
        metrics.GetProperty("requests_per_second").TryGetProperty("total", out _).Should().BeFalse();
        var point = metrics.GetProperty("requests_per_second").GetProperty("series")[0];
        point.GetProperty("t").GetInt64().Should().Be(1760000000);
        point.GetProperty("v").GetDouble().Should().Be(1.5);
    }
}
