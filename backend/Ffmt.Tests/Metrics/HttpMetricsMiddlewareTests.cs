using System.Net.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Ffmt.Tests.Metrics;

public sealed class HttpMetricsMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpMetricsMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_metrics_returns_prometheus_exposition_after_a_health_hit()
    {
        var client = _factory.CreateClient();

        var health = await client.GetAsync("/health");
        health.IsSuccessStatusCode.Should().BeTrue();

        var metrics = await client.GetAsync("/metrics");
        metrics.IsSuccessStatusCode.Should().BeTrue();

        var body = await metrics.Content.ReadAsStringAsync();
        body.Should().Contain("ffmt_http_requests_total");
        body.Should().Contain("method=\"GET\"");
        body.Should().Contain("status=\"200\"");
    }

    [Fact]
    public async Task Metrics_endpoint_is_unauthenticated_and_returns_text_plain()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/metrics");
        resp.IsSuccessStatusCode.Should().BeTrue();
        resp.Content.Headers.ContentType?.MediaType.Should().StartWith("text/plain");
    }
}
