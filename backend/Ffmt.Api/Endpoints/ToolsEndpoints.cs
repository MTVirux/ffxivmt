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

            // Sequential per-instance Garland calls to avoid hammering.
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

            // Universalis caps multi-id lookups around 100.
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

        // Score = (min_price * velocity / cost) * daily_market_cap_percent — gil-per-cost
        // efficiency weighted by the item's share of the daily market cap.
        group.MapGet("/currency_efficiency_calculator", async (
            string? search_term,
            string? location,
            string? request_id,
            IElasticItemSearch elastic,
            IItemStore items,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<CurrencyEfficiencyLog> logger,
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
            var currency = hits.FirstOrDefault();
            if (currency is null)
            {
                logger.LogWarning("currency_efficiency_calculator [{RequestId}] no Elastic hit for {Term}.", requestId, search_term);
                return Results.Json(
                    new { status = false, message = "No currency matched the search term" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            var listings = await garland.GetItemTradeCurrencyAsync(currency.Id, ct);
            if (listings.Count == 0)
            {
                return Results.Json(
                    new { status = false, message = "Garland reports no tradeCurrency listings for this item" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            var marketableIds = (await items.GetMarketableIdsAsync(ct)).ToHashSet();
            var byItemId = new Dictionary<int, GarlandTradeCurrencyListing>();
            foreach (var l in listings)
            {
                if (!marketableIds.Contains(l.ItemId)) continue;
                byItemId.TryAdd(l.ItemId, l);
            }

            if (byItemId.Count == 0)
            {
                return Results.Json(
                    new { status = false, message = "All trade-currency items are untradable" },
                    statusCode: StatusCodes.Status404NotFound);
            }

            // Universalis caps multi-id lookups around 50.
            var mb = new Dictionary<int, UniversalisMarketBoardListing>();
            foreach (var chunk in byItemId.Keys.Chunk(50))
            {
                var partial = await universalis.GetMarketBoardDataAsync(location, chunk, ct);
                foreach (var (id, l) in partial) mb[id] = l;
            }

            var raw = new List<RawCurrencyRow>(byItemId.Count);
            foreach (var (itemId, listing) in byItemId)
            {
                if (!mb.TryGetValue(itemId, out var market)) continue;

                var item = await items.GetAsync(itemId, ct);
                var name = item?.Name ?? string.Empty;
                var currencyItem = await items.GetAsync(listing.CurrencyId, ct);
                var currencyName = currencyItem?.Name ?? string.Empty;

                var medianStack = MedianStackSize(market.StackSizeHistogram);
                var dailyMarketCap = (long)Math.Round(market.RegularSaleVelocity * medianStack * market.MinPrice);
                var rawScore = listing.CurrencyAmount > 0
                    ? (double)market.MinPrice * market.RegularSaleVelocity / listing.CurrencyAmount
                    : 0d;

                raw.Add(new RawCurrencyRow(
                    Id: itemId,
                    Name: name,
                    Price: listing.CurrencyAmount,
                    CurrencyId: listing.CurrencyId,
                    CurrencyName: currencyName,
                    MinPrice: market.MinPrice,
                    RegularSaleVelocity: market.RegularSaleVelocity,
                    MedianStackSize: medianStack,
                    DailyMarketCap: dailyMarketCap,
                    RawScore: rawScore));
            }

            var totalGil = raw.Sum(r => r.DailyMarketCap);
            var rows = new List<CurrencyEfficiencyRow>(raw.Count);
            foreach (var r in raw)
            {
                var pct = totalGil > 0 ? (double)r.DailyMarketCap / totalGil * 100d : 0d;
                var score = r.RawScore * pct;
                rows.Add(new CurrencyEfficiencyRow(
                    Id: r.Id,
                    Name: r.Name,
                    Price: r.Price,
                    CurrencyId: r.CurrencyId,
                    CurrencyName: r.CurrencyName,
                    MinPrice: r.MinPrice,
                    RegularSaleVelocity: Math.Round(r.RegularSaleVelocity * 100d) / 100d,
                    MedianStackSize: r.MedianStackSize,
                    DailyMarketCap: r.DailyMarketCap,
                    DailyMarketCapPercent: Math.Round(pct * 100d) / 100d,
                    FfmtScore: Math.Round(score * 100d) / 100d));
            }

            var sorted = rows.OrderByDescending(r => r.FfmtScore).ToList();

            logger.LogInformation("currency_efficiency_calculator [{RequestId}] {Item} on {Location}: {Rows} rows.",
                requestId, currency.Name, location, sorted.Count);

            return Results.Ok(new
            {
                status = true,
                item_name = currency.Name,
                item_id = currency.Id,
                location,
                request_id = requestId,
                data = sorted,
            });
        });

        return app;
    }

    private static readonly string[] InstanceTypes = ["Dungeons", "Trials", "Raids"];

    private static int MedianStackSize(IReadOnlyDictionary<int, int> histogram)
    {
        if (histogram.Count == 0) return 0;
        // Sort first; without it the "median" would depend on dictionary iteration order.
        var flat = new List<int>();
        foreach (var (size, occ) in histogram)
        {
            for (var i = 0; i < occ; i++) flat.Add(size);
        }
        if (flat.Count == 0) return 0;
        flat.Sort();
        return flat[flat.Count / 2];
    }

    private sealed record ProfitRow(int Id, string Name, int MinPrice, double RegularSaleVelocity, double FfmtScore);

    private sealed record InstanceRow(int Id, string Name, string Type, int? MinLvl, int? MaxLvl, IReadOnlyList<InstanceLootRow> MarketableItems);

    private sealed record InstanceLootRow(int Id, string Name, int MinPrice, double RegularSaleVelocity);

    private sealed record CurrencyEfficiencyRow(
        int Id,
        string Name,
        int Price,
        int CurrencyId,
        string CurrencyName,
        int MinPrice,
        double RegularSaleVelocity,
        int MedianStackSize,
        long DailyMarketCap,
        double DailyMarketCapPercent,
        double FfmtScore);

    private sealed record RawCurrencyRow(
        int Id,
        string Name,
        int Price,
        int CurrencyId,
        string CurrencyName,
        int MinPrice,
        double RegularSaleVelocity,
        int MedianStackSize,
        long DailyMarketCap,
        double RawScore);

    private sealed class ItemProductProfitLog;
    private sealed class InstanceProfitLog;
    private sealed class CurrencyEfficiencyLog;
}
