using System.Net;
using System.Net.Http;
using Ffmt.Core.Models;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Ffmt.Tests.Metrics;

public sealed class HttpMetricsMiddlewareTests : IClassFixture<HttpMetricsMiddlewareTests.Factory>
{
    // Makes /api/v1/worlds throw an exception no handler maps, or cancel when the caller hung up.
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var worlds = Substitute.For<IWorldStore>();
            worlds.GetAllAsync(Arg.Any<CancellationToken>()).Returns(ci =>
            {
                var ct = ci.Arg<CancellationToken>();
                return ct.IsCancellationRequested
                    ? Task.FromCanceled<IReadOnlyList<World>>(ct)
                    : Task.FromException<IReadOnlyList<World>>(new InvalidOperationException("boom"));
            });
            builder.ConfigureTestServices(services => services.AddSingleton(worlds));
        }
    }

    private readonly Factory _factory;

    public HttpMetricsMiddlewareTests(Factory factory)
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

    [Fact]
    public async Task Unhandled_exception_is_counted_as_500()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/v1/worlds");
        resp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        (await RequestCountLines()).Should().Contain(l => l.Contains("/api/v1/worlds") && l.Contains("status=\"500\""));
    }

    [Fact]
    public async Task Request_aborted_by_the_client_is_counted_as_499()
    {
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = "/api/v1/worlds";
            c.RequestAborted = new CancellationToken(canceled: true);
        });
        context.Response.StatusCode.Should().Be(StatusCodes.Status499ClientClosedRequest);

        (await RequestCountLines()).Should().Contain(l => l.Contains("/api/v1/worlds") && l.Contains("status=\"499\""));
    }

    private async Task<IEnumerable<string>> RequestCountLines()
    {
        var body = await _factory.CreateClient().GetStringAsync("/metrics");
        return body.Split('\n').Where(l => l.StartsWith("ffmt_http_requests_total{", StringComparison.Ordinal));
    }
}
