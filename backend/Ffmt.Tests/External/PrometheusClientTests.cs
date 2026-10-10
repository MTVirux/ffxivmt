using System.Net;
using Ffmt.Core.External;

namespace Ffmt.Tests.External;

public sealed class PrometheusClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public Uri? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static (PrometheusClient Client, StubHandler Handler) Create(string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(status, body);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://prom:9090/") };
        return (new PrometheusClient(http), handler);
    }

    private static string Vector(string value) =>
        $$$"""{"status":"success","data":{"resultType":"vector","result":[{"metric":{},"value":[1760000000.5,"{{{value}}}"]}]}}""";

    [Fact]
    public async Task QueryAsync_returns_the_first_sample()
    {
        var (client, handler) = Create(Vector("12.5"));

        var value = await client.QueryAsync("sum(rate(x[5m]))");

        value.Should().Be(12.5);
        handler.LastRequest!.AbsolutePath.Should().Be("/api/v1/query");
        handler.LastRequest.Query.Should().Contain("query=sum%28rate%28x%5B5m%5D%29%29");
    }

    [Fact]
    public async Task QueryAsync_returns_null_for_an_empty_result()
    {
        var (client, _) = Create("""{"status":"success","data":{"resultType":"vector","result":[]}}""");

        (await client.QueryAsync("absent_metric")).Should().BeNull();
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("+Inf")]
    [InlineData("-Inf")]
    public async Task QueryAsync_returns_null_for_non_finite_values(string raw)
    {
        var (client, _) = Create(Vector(raw));

        (await client.QueryAsync("q")).Should().BeNull();
    }

    [Fact]
    public async Task QueryRangeAsync_parses_the_first_series()
    {
        var (client, handler) = Create(
            """{"status":"success","data":{"resultType":"matrix","result":[{"metric":{},"values":[[1760000000,"1"],[1760000900,"NaN"]]}]}}""");
        var end = DateTimeOffset.FromUnixTimeSeconds(1760086400);

        var points = await client.QueryRangeAsync("q", end.AddHours(-24), end, TimeSpan.FromMinutes(15));

        points.Should().Equal(new SeriesPoint(1760000000, 1), new SeriesPoint(1760000900, null));
        handler.LastRequest!.AbsolutePath.Should().Be("/api/v1/query_range");
        handler.LastRequest.Query.Should().Contain("start=1760000000").And.Contain("end=1760086400").And.Contain("step=900");
    }

    [Fact]
    public async Task QueryRangeAsync_returns_empty_for_an_empty_result()
    {
        var (client, _) = Create("""{"status":"success","data":{"resultType":"matrix","result":[]}}""");

        var end = DateTimeOffset.UtcNow;
        (await client.QueryRangeAsync("q", end.AddHours(-1), end, TimeSpan.FromMinutes(15))).Should().BeEmpty();
    }

    [Fact]
    public async Task Http_error_throws()
    {
        var (client, _) = Create("""{"status":"error","error":"bad query"}""", HttpStatusCode.BadRequest);

        await client.Invoking(c => c.QueryAsync("q")).Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Non_success_status_throws()
    {
        var (client, _) = Create("""{"status":"error","error":"bad query"}""");

        await client.Invoking(c => c.QueryAsync("q")).Should().ThrowAsync<InvalidOperationException>();
    }
}
