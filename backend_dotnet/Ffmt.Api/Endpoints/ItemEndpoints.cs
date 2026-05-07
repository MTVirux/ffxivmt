using System.Globalization;
using Ffmt.Core.Storage.Elastic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

public static class ItemEndpoints
{
    public static IEndpointRouteBuilder MapItemEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/item");

        group.MapGet("/get_by_name", async (string? name, IElasticItemSearch search, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Results.Json(
                    new { status = false, message = "No name provided" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            // Match the legacy ucwords(strtolower(...)) normalisation so callers built around
            // the PHP API see the same behaviour: "MYTHRIL ingot" → "Mythril Ingot".
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
