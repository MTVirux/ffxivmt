using Ffmt.Core.Mannequin;
using Ffmt.Core.Models;
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
            bool? hq,
            int? min_unit_price,
            string? buyer_name,
            MannequinSalesReader reader,
            CancellationToken ct) =>
        {
            if (before is < 0 || before > MaxUnixMs)
            {
                return ApiResults.Fail("before must be unix milliseconds", StatusCodes.Status400BadRequest);
            }

            var query = new MannequinFeedQuery(
                string.IsNullOrWhiteSpace(target_location) ? null : target_location,
                before is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(before.Value),
                Math.Clamp(limit ?? 50, 1, 200),
                hq,
                Math.Max(0, min_unit_price ?? 0),
                string.IsNullOrWhiteSpace(buyer_name) ? null : buyer_name.Trim());

            var page = await reader.GetAsync(query, ct);
            if (page is null)
            {
                return ApiResults.Fail($"Unknown location '{target_location}'", StatusCodes.Status404NotFound);
            }

            return Results.Ok(new
            {
                status = true,
                message = "Mannequin sales retrieved successfully",
                data = page.Sales.Select(SaleRow.From),
                next_before = page.NextBefore?.ToUnixTimeMilliseconds(),
            });
        });

        app.MapGet("/api/v1/mannequin_sales/all", async (MannequinSalesReader reader, CancellationToken ct) =>
        {
            var sales = await reader.GetAllAsync(ct);

            return Results.Ok(new
            {
                status = true,
                message = "Mannequin sales retrieved successfully",
                complete = sales is not null,
                data = (sales ?? []).Select(SaleRow.From),
            });
        });

        return app;
    }

    private sealed record SaleRow(
        int ItemId, int WorldId, string BuyerName, DateTimeOffset SaleTime, bool Hq, int Quantity, int UnitPrice, long TotalPrice)
    {
        public static SaleRow From(Sale s) =>
            new(s.ItemId, s.WorldId, s.BuyerName, s.SaleTime, s.Hq, s.Quantity, s.UnitPrice, (long)s.Quantity * s.UnitPrice);
    }
}
