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
        // Byte-compatible with the legacy PHP `/status` controller: {status: "Scylla is up"|"Scylla is down", code}.
        // 10-second IMemoryCache matches the PHP `cache->save(... 10)` window. The richer
        // `/health/ready` endpoint covers the multi-dependency case for newer consumers.
        app.MapGet("/status", async (
            ScyllaHealthCheck scyllaHealth,
            IMemoryCache cache,
            HttpContext context,
            CancellationToken ct) =>
        {
            if (cache.TryGetValue<StatusResponse>(CacheKey, out var cached) && cached is not null)
            {
                context.Response.StatusCode = cached.Code;
                return Results.Json(cached);
            }

            var result = await scyllaHealth.CheckHealthAsync(new HealthCheckContext(), ct);
            var response = result.Status == HealthStatus.Healthy
                ? new StatusResponse("Scylla is up", 200)
                : new StatusResponse("Scylla is down", 500);

            cache.Set(CacheKey, response, CacheTtl);
            context.Response.StatusCode = response.Code;
            return Results.Json(response);
        });

        return app;
    }

    private sealed record StatusResponse(string Status, int Code);
}
