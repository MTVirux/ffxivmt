using Ffmt.Core.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

public static class WorldsEndpoints
{
    public static IEndpointRouteBuilder MapWorldsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/worlds");

        group.MapGet("", async (WorldStructureService svc, CancellationToken ct) =>
        {
            var structure = await svc.GetAsync(ct);
            if (structure.Count == 0)
            {
                return Results.Json(
                    new { status = false, message = "No worlds found" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            return Results.Ok(new
            {
                status = true,
                message = "Worlds retrieved successfully",
                data = structure,
            });
        });

        return app;
    }
}
