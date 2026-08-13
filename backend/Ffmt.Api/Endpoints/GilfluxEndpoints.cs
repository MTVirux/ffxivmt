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
                    return ApiResults.Fail("No target location provided", StatusCodes.Status400BadRequest);
                }

                var result = await reader.GetByLocationAsync(target_location, ParseTruthy(crafted_only), ct);
                if (result is null)
                {
                    return ApiResults.Fail($"Unknown location '{target_location}'", StatusCodes.Status404NotFound);
                }

                return Envelope(
                    result.FromCache ? "Retrieved from cache" : "Success",
                    result.Rankings,
                    opts,
                    request_id);
            })
            .WithRequestTimeout(TimeSpan.FromSeconds(120));

        group.MapGet("/item/{item_id:int}", async (
            int item_id,
            string? target_location,
            string? request_id,
            IGilfluxRankingStore store,
            GilfluxRankingReader reader,
            IOptions<GilfluxOptions> opts,
            CancellationToken ct) =>
        {
            if (item_id <= 0)
            {
                return ApiResults.Fail("Invalid item_id", StatusCodes.Status400BadRequest);
            }

            IReadOnlyList<EnrichedGilfluxRanking>? rankings;
            if (string.IsNullOrWhiteSpace(target_location))
            {
                rankings = await reader.EnrichAsync(await store.GetByItemAsync(item_id, ct), ct);
            }
            else
            {
                rankings = await reader.GetByItemAndLocationAsync(item_id, target_location, ct);
                if (rankings is null)
                {
                    return ApiResults.Fail($"Unknown location '{target_location}'", StatusCodes.Status404NotFound);
                }
            }

            return Envelope("Success", rankings, opts, request_id);
        });

        return app;
    }

    // Carries two fields ApiResults.Ok does not, so it stays hand-written.
    private static IResult Envelope(
        string message,
        IReadOnlyList<EnrichedGilfluxRanking> data,
        IOptions<GilfluxOptions> opts,
        string? request_id) =>
        Results.Ok(new
        {
            status = true,
            message,
            data,
            gilflux_timeframe_in_ms = opts.Value.TimeframesMs,
            request_id,
        });

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
