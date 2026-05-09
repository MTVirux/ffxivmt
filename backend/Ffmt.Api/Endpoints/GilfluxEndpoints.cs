using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ffmt.Api.Endpoints;

public static class GilfluxEndpoints
{
    public static IEndpointRouteBuilder MapGilfluxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/gilflux");

        group.MapGet("", async (
                string? target_location,
                string? crafted_only,
                string? request_id,
                GilfluxRankingReader reader,
                IOptions<GilfluxOptions> opts,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(target_location))
                {
                    return Results.Json(
                        new { status = false, message = "No target location provided" },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var crafted = ParseTruthy(crafted_only);

                var result = await reader.GetByLocationAsync(target_location, crafted, ct);
                if (result is null)
                {
                    return Results.Json(
                        new { status = false, message = $"Unknown location '{target_location}'" },
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Ok(new
                {
                    status = true,
                    message = result.FromCache ? "Retrieved from cache" : "Success",
                    data = result.Rankings,
                    gilflux_timeframe_in_ms = opts.Value.TimeframesMs,
                    request_id,
                });
            })
            .WithRequestTimeout(TimeSpan.FromSeconds(120));

        group.MapGet("/item/{item_id:int}", async (
            int item_id,
            string? target_location,
            string? request_id,
            IGilfluxRankingStore store,
            LocationResolver resolver,
            GilfluxRankingReader reader,
            IOptions<GilfluxOptions> opts,
            CancellationToken ct) =>
        {
            if (item_id <= 0)
            {
                return Results.Json(
                    new { status = false, message = "Invalid item_id" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            IEnumerable<Ffmt.Core.Models.GilfluxRanking> rawRankings;

            if (string.IsNullOrWhiteSpace(target_location))
            {
                rawRankings = await store.GetByItemAsync(item_id, ct);
            }
            else
            {
                var resolution = await resolver.ResolveAsync(target_location, ct);
                if (resolution is null)
                {
                    return Results.Json(
                        new { status = false, message = $"Unknown location '{target_location}'" },
                        statusCode: StatusCodes.Status404NotFound);
                }

                rawRankings = resolution.Kind switch
                {
                    LocationKind.World      => await store.GetByItemAndWorldAsync(item_id, resolution.WorldId!.Value, ct),
                    LocationKind.Datacenter => (await store.GetByItemAsync(item_id, ct))
                        .Where(r => r.WorldId is not null), // narrowing happens in the post-enrich filter below
                    LocationKind.Region     => (await store.GetByItemAsync(item_id, ct))
                        .Where(r => r.WorldId is not null),
                    _ => Array.Empty<Ffmt.Core.Models.GilfluxRanking>(),
                };

                // Enrich first so DC/region filtering can match against world metadata.
                var enrichedAll = await reader.EnrichAsync(rawRankings, ct);
                var filtered = resolution.Kind switch
                {
                    LocationKind.Datacenter => enrichedAll.Where(r => string.Equals(r.Datacenter, resolution.CanonicalName, StringComparison.OrdinalIgnoreCase)).ToList(),
                    LocationKind.Region     => enrichedAll.Where(r => string.Equals(r.Region, resolution.CanonicalName, StringComparison.OrdinalIgnoreCase)).ToList(),
                    _ => (IEnumerable<EnrichedGilfluxRanking>)enrichedAll,
                };

                return Results.Ok(new
                {
                    status = true,
                    message = "Success",
                    data = filtered,
                    gilflux_timeframe_in_ms = opts.Value.TimeframesMs,
                    request_id,
                });
            }

            var enriched = await reader.EnrichAsync(rawRankings, ct);
            return Results.Ok(new
            {
                status = true,
                message = "Success",
                data = enriched,
                gilflux_timeframe_in_ms = opts.Value.TimeframesMs,
                request_id,
            });
        });

        return app;
    }

    private static bool ParseTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase) || trimmed == "0")
        {
            return false;
        }

        return true;
    }
}
