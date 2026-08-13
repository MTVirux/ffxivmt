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
        app.MapGet("/api/v1/search_buyer", async (
                string? buyer_name,
                string? world,
                ISaleStore sales,
                IWorldStore worlds,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(buyer_name))
                {
                    return ApiResults.Fail(
                        "GET request failed, please try again. Missing: buyer_name field",
                        StatusCodes.Status400BadRequest);
                }

                int? worldId = null;
                if (!string.IsNullOrWhiteSpace(world))
                {
                    var resolved = await worlds.GetByNameAsync(world, ct);
                    worldId = resolved?.Id;
                }

                var history = await sales.SearchBuyerAsync(buyer_name, worldId, ct);

                // sales_by_buyer does not carry Hq/OnMannequin; project explicitly so we
                // don't ship zeroed fields the frontend would misread as legitimate
                // values. Quantity 0 means the row predates the quantity/unit_price
                // columns, so it surfaces as null rather than a real zero.
                var projection = history.Select(s => new
                {
                    s.ItemId,
                    s.WorldId,
                    s.BuyerName,
                    s.SaleTime,
                    Quantity = s.Quantity == 0 ? (int?)null : s.Quantity,
                    TotalPrice = s.Quantity == 0 ? (long?)null : (long)s.Quantity * s.UnitPrice,
                });

                return Results.Ok(new
                {
                    status = true,
                    data = projection,
                });
            })
            .WithRequestTimeout(TimeSpan.FromSeconds(300));

        return app;
    }
}
