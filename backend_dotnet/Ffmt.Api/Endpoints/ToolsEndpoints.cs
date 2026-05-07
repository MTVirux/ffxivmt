using System.Security.Cryptography;
using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api.Endpoints;

public static class ToolsEndpoints
{
    public static IEndpointRouteBuilder MapToolsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tools");

        // /item_product_profit_calculator?search_term=<name>&location=<world|dc|region>&request_id=<opt>
        // Resolves the item via Elasticsearch, walks Garland partials for the recipe components,
        // fetches Universalis MB summaries for the union, and returns each entry's
        // min_price * regularSaleVelocity score sorted desc.
        group.MapGet("/item_product_profit_calculator", async (
            string? search_term,
            string? location,
            string? request_id,
            IElasticItemSearch elastic,
            IItemStore items,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<ItemProductProfitLog> logger,
            CancellationToken ct) =>
        {
            using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ApiInfo });

            if (string.IsNullOrWhiteSpace(search_term))
            {
                return Results.Json(
                    new { status = false, message = "GET request failed, please try again. Missing: search_term field" },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            if (string.IsNullOrWhiteSpace(location))
            {
                return Results.Json(
                    new { status = false, message = "GET request failed, please try again. Missing: location field" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var requestId = string.IsNullOrWhiteSpace(request_id)
                ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
                : request_id!;

            var hits = await elastic.SearchByNameAsync(search_term, size: 1, ct);
            var top = hits.FirstOrDefault();
            if (top is null)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] no Elastic hit for {Term}.", requestId, search_term);
                return Results.Json(
                    new { status = false, message = "No item matched the search term" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            var garlandDetail = await garland.GetItemDetailAsync(top.Id, ct);
            if (garlandDetail is null)
            {
                return Results.Json(
                    new { status = false, message = "Garland lookup failed" },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var idsToFetch = new List<int>(garlandDetail.RelatedItemIds.Count + 1);
            idsToFetch.AddRange(garlandDetail.RelatedItemIds);
            idsToFetch.Add(top.Id);

            var mb = await universalis.GetMarketBoardDataAsync(location, idsToFetch, ct);
            if (mb.Count == 0)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] Universalis returned no rows for {Location}.", requestId, location);
                return Results.Json(
                    new { status = false, message = "Could not fetch MB Data from Universalis. Please try again later." },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var rows = new List<ProfitRow>(mb.Count);
            foreach (var (id, listing) in mb)
            {
                var item = await items.GetAsync(id, ct);
                var name = item?.Name ?? string.Empty;
                rows.Add(new ProfitRow(
                    Id: id,
                    Name: name,
                    MinPrice: listing.MinPrice,
                    RegularSaleVelocity: listing.RegularSaleVelocity,
                    FfmtScore: listing.MinPrice * listing.RegularSaleVelocity));
            }

            var sorted = rows.OrderByDescending(r => r.FfmtScore).ToList();

            logger.LogInformation("item_product_profit_calculator [{RequestId}] {Item} on {Location}: {Rows} rows.",
                requestId, top.Name, location, sorted.Count);

            return Results.Ok(new
            {
                status = true,
                item_name = top.Name,
                item_id = top.Id,
                location,
                request_id = requestId,
                data = sorted,
            });
        });

        return app;
    }

    /// <summary>
    /// Result row for the profit calculator. Field names map to <c>id</c>, <c>name</c>,
    /// <c>min_price</c>, <c>regular_sale_velocity</c>, <c>ffmt_score</c> via the global
    /// snake_case policy.
    /// </summary>
    private sealed record ProfitRow(int Id, string Name, int MinPrice, double RegularSaleVelocity, double FfmtScore);

    private sealed class ItemProductProfitLog;
}
