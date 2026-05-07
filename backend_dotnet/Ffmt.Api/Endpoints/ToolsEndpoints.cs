using System.Security.Cryptography;
using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Ffmt.Api.Endpoints;

public static class ToolsEndpoints
{
    public static IEndpointRouteBuilder MapToolsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tools");

        // /item_product_profit_calculator?search_term=<name>&location=<world|dc|region>&request_id=<opt>
        // Resolves the item via Elasticsearch, walks Garland partials for the recipe components,
        // fetches Universalis MB summaries for the union, and returns each entry's
        // min_price * regularSaleVelocity score sorted desc.
        group.MapGet("/item_product_profit_calculator", async (
            string? search_term,
            string? location,
            string? request_id,
            IElasticItemSearch elastic,
            IItemStore items,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<ItemProductProfitLog> logger,
            CancellationToken ct) =>
        {
            using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ApiInfo });

            if (string.IsNullOrWhiteSpace(search_term))
            {
                return Results.Json(
                    new { status = false, message = "GET request failed, please try again. Missing: search_term field" },
                    statusCode: StatusCodes.Status400BadRequest);
            }
            if (string.IsNullOrWhiteSpace(location))
            {
                return Results.Json(
                    new { status = false, message = "GET request failed, please try again. Missing: location field" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var requestId = string.IsNullOrWhiteSpace(request_id)
                ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
                : request_id!;

            var hits = await elastic.SearchByNameAsync(search_term, size: 1, ct);
            var top = hits.FirstOrDefault();
            if (top is null)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] no Elastic hit for {Term}.", requestId, search_term);
                return Results.Json(
                    new { status = false, message = "No item matched the search term" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            var garlandDetail = await garland.GetItemDetailAsync(top.Id, ct);
            if (garlandDetail is null)
            {
                return Results.Json(
                    new { status = false, message = "Garland lookup failed" },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var idsToFetch = new List<int>(garlandDetail.RelatedItemIds.Count + 1);
            idsToFetch.AddRange(garlandDetail.RelatedItemIds);
            idsToFetch.Add(top.Id);

            var mb = await universalis.GetMarketBoardDataAsync(location, idsToFetch, ct);
            if (mb.Count == 0)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] Universalis returned no rows for {Location}.", requestId, location);
                return Results.Json(
                    new { status = false, message = "Could not fetch MB Data from Universalis. Please try again later." },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var rows = new List<ProfitRow>(mb.Count);
            foreach (var (id, listing) in mb)
            {
                var item = await items.GetAsync(id, ct);
                var name = item?.Name ?? string.Empty;
                rows.Add(new ProfitRow(
                    Id: id,
                    Name: name,
                    MinPrice: listing.MinPrice,
                    RegularSaleVelocity: listing.RegularSaleVelocity,
                    FfmtScore: listing.MinPrice * listing.RegularSaleVelocity));
            }

            var sorted = rows.OrderByDescending(r => r.FfmtScore).ToList();

            logger.LogInformation("item_product_profit_calculator [{RequestId}] {Item} on {Location}: {Rows} rows.",
                requestId, top.Name, location, sorted.Count);

            return Results.Ok(new
            {
                status = true,
                item_name = top.Name,
                item_id = top.Id,
                location,
                request_id = requestId,
                data = sorted,
            });
        });

        // /instance_profit_calculator?location=<world|dc|region>
        // Walks Garland's instance browse, filters to dungeons/trials/raids minus Savage/Ultimate
        // variants, gathers marketable loot ids per instance, then makes ONE Universalis multi-id
        // call (chunked at 100) for the union of ids — replacing the legacy N+1 per-item loop.
        //
        // Port-and-fix: the PHP filter `strpos($instance["n"], "Ultimate" || strpos($instance))`
        // is an operator-precedence bug that always evaluates the second strpos against a boolean.
        // Fixed here to a clean substring check on the instance name.
        group.MapGet("/instance_profit_calculator", async (
            string? location,
            IItemStore items,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<InstanceProfitLog> logger,
            CancellationToken ct) =>
        {
            using var _ = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = LogChannels.ApiInfo });

            if (string.IsNullOrWhiteSpace(location))
            {
                return Results.Json(
                    new { status = false, message = "No location provided" },
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var summaries = await garland.GetAllInstancesAsync(ct);
            var marketableIds = new HashSet<int>(await items.GetMarketableIdsAsync(ct));

            var validInstances = summaries
                .Where(i => InstanceTypes.Contains(i.Type, StringComparer.OrdinalIgnoreCase))
                .Where(i => !i.Name.Contains("Savage", StringComparison.Ordinal)
                         && !i.Name.Contains("Ultimate", StringComparison.Ordinal))
                .ToList();

            // Per-instance loot: one Garland call per instance, sequential so we don't hammer.
            var instanceLoot = new Dictionary<int, List<int>>();
            var allMarketableLootIds = new HashSet<int>();
            foreach (var instance in validInstances)
            {
                ct.ThrowIfCancellationRequested();
                var detail = await garland.GetInstanceAsync(instance.Id, ct);
                if (detail is null) continue;

                var marketable = detail.LootItemIds.Where(marketableIds.Contains).Distinct().ToList();
                if (marketable.Count == 0) continue;

                instanceLoot[instance.Id] = marketable;
                foreach (var id in marketable) allMarketableLootIds.Add(id);
            }

            // Single batched MB lookup for the union; chunk at Universalis's effective ~100/id limit.
            var listings = new Dictionary<int, UniversalisMarketBoardListing>();
            foreach (var chunk in allMarketableLootIds.Chunk(100))
            {
                var partial = await universalis.GetMarketBoardDataAsync(location, chunk, ct);
                foreach (var (id, listing) in partial) listings[id] = listing;
            }

            var rows = new List<InstanceRow>(validInstances.Count);
            foreach (var instance in validInstances)
            {
                if (!instanceLoot.TryGetValue(instance.Id, out var lootIds)) continue;

                var lootRows = new List<InstanceLootRow>(lootIds.Count);
                foreach (var lootId in lootIds)
                {
                    if (!listings.TryGetValue(lootId, out var listing)) continue;
                    var item = await items.GetAsync(lootId, ct);
                    lootRows.Add(new InstanceLootRow(
                        Id: lootId,
                        Name: item?.Name ?? string.Empty,
                        MinPrice: listing.MinPrice,
                        RegularSaleVelocity: listing.RegularSaleVelocity));
                }

                rows.Add(new InstanceRow(
                    Id: instance.Id,
                    Name: instance.Name,
                    Type: instance.Type,
                    MinLvl: instance.MinLevel,
                    MaxLvl: instance.MaxLevel,
                    MarketableItems: lootRows));
            }

            logger.LogInformation("instance_profit_calculator on {Location}: {Instances} instances, {Items} unique items.",
                location, rows.Count, allMarketableLootIds.Count);

            return Results.Ok(new { status = true, data = rows });
        });

        return app;
    }

    private static readonly string[] InstanceTypes = ["Dungeons", "Trials", "Raids"];

    /// <summary>
    /// Result row for the profit calculator. Field names map to <c>id</c>, <c>name</c>,
    /// <c>min_price</c>, <c>regular_sale_velocity</c>, <c>ffmt_score</c> via the global
    /// snake_case policy.
    /// </summary>
    private sealed record ProfitRow(int Id, string Name, int MinPrice, double RegularSaleVelocity, double FfmtScore);

    private sealed record InstanceRow(int Id, string Name, string Type, int? MinLvl, int? MaxLvl, IReadOnlyList<InstanceLootRow> MarketableItems);

    private sealed record InstanceLootRow(int Id, string Name, int MinPrice, double RegularSaleVelocity);

    private sealed class ItemProductProfitLog;
    private sealed class InstanceProfitLog;
}
