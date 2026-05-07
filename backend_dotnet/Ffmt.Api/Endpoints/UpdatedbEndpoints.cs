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

        // Byte-compatible with PHP `python_request_post`. The Python sales importer hits this
        // with batches of Universalis v2 history responses; we transform → bulk-insert into
        // `sales` → kick a gilflux ranking refresh per `(world, item)` pair touched.
        group.MapPost("/python_request", async (
                PythonRequestPayload? payload,
                IItemStore items,
                IWorldStore worlds,
                ISaleStore sales,
                IGilfluxRankingStore rankings,
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
                    await rankings.UpdateRankingAsync(worldId, itemId, ct);
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
            .WithRequestTimeout(System.Threading.Timeout.InfiniteTimeSpan); // PHP: set_time_limit(0)

        // Byte-compatible with PHP `gilflux_ranking_update_get`. The ws_worker hits this per
        // `(world, item)` pair after every sales/add message it consumes.
        group.MapGet("/gilflux_ranking_update/{world_id:int}/{item_id:int}", async (
            int world_id,
            int item_id,
            IGilfluxRankingStore rankings,
            ILogger<GilfluxRankingUpdateLog> logger,
            CancellationToken ct) =>
        {
            using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ScyllaGilflux });

            await rankings.UpdateRankingAsync(world_id, item_id, ct);
            logger.LogInformation("gilflux_ranking_update: refreshed (world={WorldId}, item={ItemId}).", world_id, item_id);
            return Results.Ok(new { status = true });
        });

        return app;
    }

    // Marker types so each endpoint gets a distinct ILogger<T> category in Serilog output.
    private sealed class PythonRequestLog;
    private sealed class GilfluxRankingUpdateLog;
}
