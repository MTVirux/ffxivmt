using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Prometheus.DotNetRuntime;

namespace Ffmt.Core.Metrics;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Wires up prometheus-net + .NET runtime metrics. The /metrics endpoint must
    /// be mapped explicitly by the host via <see cref="MapFfmtMetrics"/>.
    /// </summary>
    public static IServiceCollection AddFfmtMetrics(this IServiceCollection services)
    {
        // Touch the catalog so every instrument registers eagerly (otherwise some
        // gauges won't appear in /metrics output until first emit, which makes
        // dashboards "blank" for new instances during cold start).
        _ = MetricsCatalog.All;

        // .NET runtime metrics: GC, thread pool, JIT, contention, exceptions.
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
    /// <summary>
    /// Maps GET /metrics on the same Kestrel port as the rest of the app.
    /// Caddy does NOT proxy /metrics, so this endpoint is internal-network-only.
    /// </summary>
    public static IEndpointConventionBuilder MapFfmtMetrics(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapMetrics("/metrics");
    }
}

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Installs the HTTP RED middleware. Must be called AFTER UseRouting / UseEndpoints
    /// route-template resolution so endpoint label is the route template, not the URL path.
    /// </summary>
    public static IApplicationBuilder UseFfmtHttpMetrics(this IApplicationBuilder app)
    {
        return app.UseHttpMetrics(options =>
        {
            options.RequestCount.Enabled = false;     // we own this counter (MetricsCatalog.HttpRequestsTotal)
            options.RequestDuration.Enabled = false;  // we own this histogram (MetricsCatalog.HttpRequestDurationSeconds)
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
