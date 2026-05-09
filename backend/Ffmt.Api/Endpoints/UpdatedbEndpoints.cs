using Ffmt.Core.Gilflux;
using Ffmt.Core.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api.Endpoints;

public static class UpdatedbEndpoints
{
    public static IEndpointRouteBuilder MapUpdatedbEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/updatedb");

        group.MapGet("/gilflux_ranking_update/{world_id:int}/{item_id:int}", async (
            int world_id,
            int item_id,
            IRankingRefresher refresher,
            ILogger<GilfluxRankingUpdateLog> logger,
            CancellationToken ct) =>
        {
            using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaGilflux });

            await refresher.RefreshAsync(world_id, item_id, ct);
            logger.LogInformation("gilflux_ranking_update: refreshed (world={WorldId}, item={ItemId}).", world_id, item_id);
            return Results.Ok(new { status = true });
        });

        return app;
    }

    private sealed class GilfluxRankingUpdateLog;
}
