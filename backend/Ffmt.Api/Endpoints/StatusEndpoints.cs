using Ffmt.Core.HealthChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Ffmt.Api.Endpoints;

public static class StatusEndpoints
{
    private const string CacheKey = "scylla_status";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status", async (
            ScyllaHealthCheck scyllaHealth,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            if (cache.TryGetValue<StatusResponse>(CacheKey, out var cached) && cached is not null)
            {
                return Results.Json(cached, statusCode: cached.Code);
            }

            var result = await scyllaHealth.CheckHealthAsync(new HealthCheckContext(), ct);
            var response = result.Status == HealthStatus.Healthy
                ? new StatusResponse("Scylla is up", 200)
                : new StatusResponse("Scylla is down", 500);

            cache.Set(CacheKey, response, CacheTtl);
            return Results.Json(response, statusCode: response.Code);
        });

        return app;
    }

    private sealed record StatusResponse(string Status, int Code);
}
