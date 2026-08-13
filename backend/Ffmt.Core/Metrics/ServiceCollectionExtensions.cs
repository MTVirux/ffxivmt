using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Prometheus.DotNetRuntime;

namespace Ffmt.Core.Metrics;

public static class ServiceCollectionExtensions
{
    // Does not map /metrics - hosts do that themselves via MapFfmtMetrics.
    public static IServiceCollection AddFfmtMetrics(this IServiceCollection services)
    {
        // Touch the catalog so every instrument registers eagerly; otherwise gauges are absent
        // from /metrics until their first emit and dashboards read blank during cold start.
        _ = MetricsCatalog.All;

        DotNetRuntimeStatsBuilder
            .Customize()
            .WithGcStats()
            .WithThreadPoolStats()
            .WithExceptionStats()
            .WithContentionStats()
            .StartCollecting();

        return services;
    }
}

public static class EndpointRouteBuilderExtensions
{
    // Shares the app's Kestrel port. Caddy does not proxy /metrics, so it stays internal-only.
    public static IEndpointConventionBuilder MapFfmtMetrics(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapMetrics("/metrics");
    }
}

public static class ApplicationBuilderExtensions
{
    // Must run after UseRouting so the endpoint label is the route template, not the raw path.
    public static IApplicationBuilder UseFfmtHttpMetrics(this IApplicationBuilder app)
    {
        return app.UseHttpMetrics(options =>
        {
            // MetricsCatalog owns the RED instruments; the built-ins would duplicate them.
            options.RequestCount.Enabled = false;
            options.RequestDuration.Enabled = false;
            options.InProgress.Enabled = false;
        }).Use(async (context, next) =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await next();
            }
            finally
            {
                sw.Stop();
                var endpoint = context.GetEndpoint()?.DisplayName
                            ?? context.Request.Path.Value
                            ?? "unknown";
                var method = context.Request.Method;
                var status = context.Response.StatusCode.ToString();

                MetricsCatalog.HttpRequestsTotal
                    .WithLabels(endpoint, method, status)
                    .Inc();
                MetricsCatalog.HttpRequestDurationSeconds
                    .WithLabels(endpoint, method)
                    .Observe(sw.Elapsed.TotalSeconds);
            }
        });
    }
}
