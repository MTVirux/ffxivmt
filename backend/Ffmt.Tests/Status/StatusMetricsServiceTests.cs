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

    private void PrometheusReturns(double? value, double? worldsTotal = 80, double? storedBatchesLast10m = 1000)
    {
        _prometheus.QueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(value);
        _prometheus.QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>()).Returns(worldsTotal);
        _prometheus.QueryAsync(StatusMetricsService.StoredBatchesLast10mQuery, Arg.Any<CancellationToken>()).Returns(storedBatchesLast10m);
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
    public async Task Down_when_no_sale_batches_were_stored()
    {
        PrometheusReturns(80);
        _prometheus.QueryAsync("sum(increase(ffmt_ws_inserts_total{result=\"ok\"}[10m]))", Arg.Any<CancellationToken>()).Returns(0);

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.State.Should().Be("down");
        response.Reasons.Should().Equal("No sales stored in the last 10 minutes");
    }

    [Theory]
    [InlineData(4.0, "operational")]
    [InlineData(5.0, "degraded")]
    public async Task Error_rate_needs_a_minimum_count_of_server_errors(double serverErrors, string state)
    {
        PrometheusReturns(80);
        _prometheus.QueryAsync(StatusMetricsService.Queries["error_rate"], Arg.Any<CancellationToken>()).Returns(0.2);
        _prometheus.QueryAsync("sum(increase(ffmt_http_requests_total{status=~\"5..\"}[5m]))", Arg.Any<CancellationToken>()).Returns(serverErrors);

        var response = await CreateService().GetAsync(CancellationToken.None);

        response.State.Should().Be(state);
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
    public async Task Concurrent_callers_share_one_build()
    {
        var worldsTotal = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);
        PrometheusReturns(80);
        _prometheus.QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>()).Returns(worldsTotal.Task);

        var first = CreateService().GetAsync(CancellationToken.None);
        var second = CreateService().GetAsync(CancellationToken.None);
        worldsTotal.SetResult(80);

        (await first).Should().BeSameAs(await second);
        await _prometheus.Received(1).QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancelled_caller_does_not_cancel_the_shared_build()
    {
        var worldsTotal = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);
        PrometheusReturns(80);
        _prometheus.QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>())
            .Returns(ci => worldsTotal.Task.WaitAsync(ci.Arg<CancellationToken>()));
        using var cts = new CancellationTokenSource();

        var cancelled = CreateService().GetAsync(cts.Token);
        await cts.CancelAsync();
        await FluentActions.Awaiting(() => cancelled).Should().ThrowAsync<OperationCanceledException>();

        worldsTotal.SetResult(80);
        (await CreateService().GetAsync(CancellationToken.None)).Available.Should().BeTrue();
        await _prometheus.Received(1).QueryAsync(StatusMetricsService.WorldsTotalQuery, Arg.Any<CancellationToken>());
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
