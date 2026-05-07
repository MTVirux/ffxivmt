using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

public static class SearchBuyerEndpoints
{
    public static IEndpointRouteBuilder MapSearchBuyerEndpoints(this IEndpointRouteBuilder app)
    {
        // PHP returned `data` as a JSON-encoded string (`json_encode($buyer_history)`); per the
        // cutover plan that double-encode is fixed here — `data` is the raw array. Razor pages
        // that consumed the old shape are updated in lockstep in Phase 7.
        app.MapGet("/api/v1/search_buyer", async (
                string? buyer_name,
                string? world,
                ISaleStore sales,
                IWorldStore worlds,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(buyer_name))
                {
                    return Results.Json(
                        new { status = false, message = "GET request failed, please try again. Missing: buyer_name field" },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                int? worldId = null;
                if (!string.IsNullOrWhiteSpace(world))
                {
                    var resolved = await worlds.GetByNameAsync(world, ct);
                    worldId = resolved?.Id;
                }

                var history = await sales.SearchBuyerAsync(buyer_name, worldId, ct);

                return Results.Ok(new
                {
                    status = true,
                    data = history,
                });
            })
            .WithRequestTimeout(TimeSpan.FromSeconds(300));

        return app;
    }
}
