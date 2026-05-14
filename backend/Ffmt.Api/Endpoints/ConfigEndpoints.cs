using Ffmt.Core.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Ffmt.Api.Endpoints;

public static class ConfigEndpoints
{
    public static IEndpointRouteBuilder MapConfigEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/config", (IOptions<GilfluxOptions> opts) =>
        {
            var timeframes = opts.Value.TimeframesMs
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToArray();

            return Results.Ok(new { status = true, data = new { gilflux_timeframes = timeframes } });
        });

        return app;
    }
}
