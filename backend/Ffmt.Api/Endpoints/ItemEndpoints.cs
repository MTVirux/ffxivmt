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
            int? world_id,
            int? limit,
            ISaleStore sales,
            CancellationToken ct) =>
        {
            if (world_id is null || world_id <= 0)
            {
                return Results.Json(
                    new { status = false, message = "world_id is required" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var n = Math.Clamp(limit ?? 100, 1, 500);
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
