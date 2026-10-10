using Ffmt.Core.GcSeals;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ffmt.Api.Endpoints;

/// <summary>Static recipe and seal data only. Market prices are fetched from Universalis by the browser.</summary>
public static class GcSealsEndpoints
{
    public static IEndpointRouteBuilder MapGcSealsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/tools/gc_seals/catalogue", async (GcSealsCatalogueService catalogue, CancellationToken ct) =>
            ApiResults.Ok("GC seals catalogue", await catalogue.GetAsync(ct)));

        return app;
    }
}
