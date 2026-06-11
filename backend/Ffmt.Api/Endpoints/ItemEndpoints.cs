using System.Globalization;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/item");

        group.MapGet("/{id:int}", async (int id, IItemStore items, CancellationToken ct) =>
        {
            var item = await items.GetAsync(id, ct);
            if (item is null)
            {
                return Results.Json(
                    new { status = false, message = "Item not found" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(new
            {
                status = true,
                message = "Item retrieved successfully",
                data = item,
            });
        });

        group.MapGet("/{id:int}/sales", async (
            int id,
            string? target_location,
            int? world_id,
            int? limit,
            ISaleStore sales,
            ItemSalesReader salesReader,
            CancellationToken ct) =>
        {
            var n = Math.Clamp(limit ?? 100, 1, 500);

            if (!string.IsNullOrWhiteSpace(target_location))
            {
                var located = await salesReader.GetByItemAndLocationAsync(id, target_location, n, ct);
                if (located is null)
                {
                    return Results.Json(
                        new { status = false, message = $"Unknown location '{target_location}'" },
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Ok(new
                {
                    status = true,
                    message = "Sales retrieved successfully",
                    data = located,
                });
            }

            if (world_id is null || world_id <= 0)
            {
                return Results.Json(
                    new { status = false, message = "world_id or target_location is required" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var data = await sales.GetByItemAndWorldAsync(id, world_id.Value, n, ct);

            return Results.Ok(new
            {
                status = true,
                message = "Sales retrieved successfully",
                data,
            });
        });

        group.MapGet("/get_by_name", async (string? name, IElasticItemSearch search, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.Json(
                    new { status = false, message = "No name provided" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var normalised = ToTitleCase(name.Trim());

            var hits = await search.SearchByNameAsync(normalised, size: 25, ct);

            return Results.Ok(new
            {
                status = true,
                message = "Name provided",
                data = hits,
            });
        });

        return app;
    }

    private static string ToTitleCase(string input) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
}
