using Ffmt.Core.Configuration;
using Ffmt.Core.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ffmt.Api.Endpoints;

public static class WorldsEndpoints
{
    public static IEndpointRouteBuilder MapWorldsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/worlds");

        group.MapGet("", async (
            WorldStructureService svc,
            IOptions<UniversalisOptions> universalis,
            CancellationToken ct) =>
        {
            var structure = WorldStructureService.FilterToRegions(
                await svc.GetAsync(ct), universalis.Value.RegionsToUse);

            if (structure.Count == 0)
            {
                return ApiResults.Fail("No worlds found", StatusCodes.Status404NotFound);
            }

            return ApiResults.Ok("Worlds retrieved successfully", structure);
        });

        return app;
    }
}
