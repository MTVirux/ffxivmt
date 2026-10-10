using Ffmt.Core.Mannequin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

public static class MannequinSalesEndpoints
{
    private static readonly long MaxUnixMs = DateTimeOffset.MaxValue.ToUnixTimeMilliseconds();

    public static IEndpointRouteBuilder MapMannequinSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/mannequin_sales", async (
            string? target_location,
            long? before,
            int? limit,
            bool? hq_only,
            int? min_unit_price,
            MannequinSalesReader reader,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(target_location))
            {
                return ApiResults.Fail("target_location is required", StatusCodes.Status400BadRequest);
            }

            if (before is < 0 || before > MaxUnixMs)
            {
                return ApiResults.Fail("before must be unix milliseconds", StatusCodes.Status400BadRequest);
            }

            var query = new MannequinFeedQuery(
                target_location,
                before is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(before.Value),
                Math.Clamp(limit ?? 50, 1, 200),
                hq_only ?? false,
                Math.Max(0, min_unit_price ?? 0));

            var page = await reader.GetAsync(query, ct);
            if (page is null)
            {
                return ApiResults.Fail($"Unknown location '{target_location}'", StatusCodes.Status404NotFound);
            }

            return Results.Ok(new
            {
                status = true,
                message = "Mannequin sales retrieved successfully",
                data = page.Sales.Select(s => new
                {
                    s.ItemId,
                    s.WorldId,
                    s.BuyerName,
                    s.SaleTime,
                    s.Hq,
                    s.Quantity,
                    s.UnitPrice,
                    TotalPrice = (long)s.Quantity * s.UnitPrice,
                }),
                next_before = page.NextBefore?.ToUnixTimeMilliseconds(),
            });
        });

        return app;
    }
}
