using Ffmt.Core.Configuration;
using Ffmt.Core.Worlds;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ffmt.Api.Endpoints;

public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/config", async (
            IOptions<GilfluxOptions> opts,
            WorldStructureService structure,
            CancellationToken ct) =>
        {
            var timeframes = opts.Value.TimeframesMs
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToArray();

            var itemNames = await structure.GetVersionedItemNamesAsync(ct);

            return Results.Ok(new
            {
                status = true,
                data = new { gilflux_timeframes = timeframes, item_names_version = itemNames.Version },
            });
        });

        return app;
    }
}
