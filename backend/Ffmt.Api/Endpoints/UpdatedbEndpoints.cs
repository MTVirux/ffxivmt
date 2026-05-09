using Ffmt.Core.Gilflux;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Scylla;
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

        group.MapPost("/python_request", async (
                PythonRequestPayload? payload,
                IItemStore items,
                IWorldStore worlds,
                ISaleStore sales,
                IRankingRefresher refresher,
                ILogger<PythonRequestLog> logger,
                CancellationToken ct) =>
            {
                using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaSales });

                if (payload is null || payload.Items is null || payload.Items.Count == 0)
                {
                    return Results.Json(
                        new { status = false, message = "POST WAS EMPTY" },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var namesById = await items.GetAllNamesAsync(ct);
                var allWorlds = await worlds.GetAllAsync(ct);
                var worldsById = allWorlds.ToDictionary(w => w.Id);

                var transform = PythonRequestTransform.Build(payload, worldsById, namesById);

                var batchResult = await sales.AddBatchAsync(transform.Sales, ct);

                foreach (var (worldId, itemId) in transform.RankingPairs)
                {
                    ct.ThrowIfCancellationRequested();
                    await refresher.RefreshAsync(worldId, itemId, ct);
                }

                logger.LogInformation(
                    "python_request: parsed {Parsed} sales, refreshed {Refreshes} (world,item) rankings.",
                    batchResult.ParsedSales, transform.RankingPairs.Count);

                return Results.Ok(new
                {
                    status = true,
                    data = batchResult,
                });
            })
            .WithRequestTimeout(System.Threading.Timeout.InfiniteTimeSpan);

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

    private sealed class PythonRequestLog;
    private sealed class GilfluxRankingUpdateLog;
}
