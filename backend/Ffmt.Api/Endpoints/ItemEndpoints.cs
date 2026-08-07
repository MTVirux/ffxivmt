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
                return ApiResults.Fail("Item not found", StatusCodes.Status404NotFound);
            }

            return ApiResults.Ok("Item retrieved successfully", item);
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
            object data;

            if (!string.IsNullOrWhiteSpace(target_location))
            {
                var located = await salesReader.GetByItemAndLocationAsync(id, target_location, n, ct);
                if (located is null)
                {
                    return ApiResults.Fail($"Unknown location '{target_location}'", StatusCodes.Status404NotFound);
                }

                data = located;
            }
            else
            {
                if (world_id is null || world_id <= 0)
                {
                    return ApiResults.Fail("world_id or target_location is required", StatusCodes.Status400BadRequest);
                }

                data = await sales.GetByItemAndWorldAsync(id, world_id.Value, n, ct);
            }

            return ApiResults.Ok("Sales retrieved successfully", data);
        });

        group.MapGet("/get_by_name", async (string? name, IElasticItemSearch search, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return ApiResults.Fail("No name provided", StatusCodes.Status400BadRequest);
            }

            var normalised = ToTitleCase(name.Trim());

            var hits = await search.SearchByNameAsync(normalised, size: 25, ct);

            return ApiResults.Ok("Name provided", hits);
        });

        return app;
    }

    private static string ToTitleCase(string input) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(input.ToLowerInvariant());
}
