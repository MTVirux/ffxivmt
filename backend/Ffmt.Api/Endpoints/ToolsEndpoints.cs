using System.Security.Cryptography;
using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Worlds;
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
            WorldStructureService structure,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<ItemProductProfitLog> logger,
            CancellationToken ct) =>
        {
            using var _ = LogChannelScope.Begin(logger, LogChannels.ApiInfo);

            var invalid = ValidateToolQuery(search_term, location, request_id, out var requestId);
            if (invalid is not null)
            {
                return invalid;
            }

            var hits = await elastic.SearchByNameAsync(search_term!, size: 1, ct);
            var top = hits.FirstOrDefault();
            if (top is null)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] no Elastic hit for {Term}.", requestId, search_term);
                return ApiResults.Fail("No item matched the search term", StatusCodes.Status404NotFound);
            }

            var garlandDetail = await garland.GetItemDetailAsync(top.Id, ct);
            if (garlandDetail is null)
            {
                return ApiResults.Fail("Garland lookup failed", StatusCodes.Status502BadGateway);
            }

            var idsToFetch = new List<int>(garlandDetail.RelatedItemIds.Count + 1);
            idsToFetch.AddRange(garlandDetail.RelatedItemIds);
            idsToFetch.Add(top.Id);

            var mb = await universalis.GetMarketBoardDataAsync(location!, idsToFetch, ct);
            if (mb.Count == 0)
            {
                logger.LogWarning("item_product_profit_calculator [{RequestId}] Universalis returned no rows for {Location}.", requestId, location);
                return ApiResults.Fail("Could not fetch MB Data from Universalis. Please try again later.", StatusCodes.Status502BadGateway);
            }

            var itemNames = await structure.GetItemNamesAsync(ct);

            var rows = new List<ProfitRow>(mb.Count);
            foreach (var (id, listing) in mb)
            {
                rows.Add(new ProfitRow(
                    Id: id,
                    Name: itemNames.TryGetValue(id, out var name) ? name : string.Empty,
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
            WorldStructureService structure,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<InstanceProfitLog> logger,
            CancellationToken ct) =>
        {
            using var _ = LogChannelScope.Begin(logger, LogChannels.ApiInfo);

            if (string.IsNullOrWhiteSpace(location))
            {
                return ApiResults.Fail("No location provided", StatusCodes.Status400BadRequest);
            }

            var summariesTask = garland.GetAllInstancesAsync(ct);
            var marketableIdsTask = structure.GetMarketableItemIdsAsync(ct);
            await Task.WhenAll(summariesTask, marketableIdsTask);

            var summaries = await summariesTask;
            var marketableIds = new HashSet<int>(await marketableIdsTask);

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

            var itemNames = await structure.GetItemNamesAsync(ct);

            var rows = new List<InstanceRow>(validInstances.Count);
            foreach (var instance in validInstances)
            {
                if (!instanceLoot.TryGetValue(instance.Id, out var lootIds)) continue;

                var lootRows = new List<InstanceLootRow>(lootIds.Count);
                foreach (var lootId in lootIds)
                {
                    if (!listings.TryGetValue(lootId, out var listing)) continue;
                    lootRows.Add(new InstanceLootRow(
                        Id: lootId,
                        Name: itemNames.TryGetValue(lootId, out var name) ? name : string.Empty,
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

        // Score = (min_price * velocity / cost) * daily_market_cap_percent - gil-per-cost
        // efficiency weighted by the item's share of the daily market cap.
        group.MapGet("/currency_efficiency_calculator", async (
            string? search_term,
            string? location,
            string? request_id,
            IElasticItemSearch elastic,
            WorldStructureService structure,
            IGarlandClient garland,
            IUniversalisClient universalis,
            ILogger<CurrencyEfficiencyLog> logger,
            CancellationToken ct) =>
        {
            using var _ = LogChannelScope.Begin(logger, LogChannels.ApiInfo);

            var invalid = ValidateToolQuery(search_term, location, request_id, out var requestId);
            if (invalid is not null)
            {
                return invalid;
            }

            var hits = await elastic.SearchByNameAsync(search_term!, size: 1, ct);
            var currency = hits.FirstOrDefault();
            if (currency is null)
            {
                logger.LogWarning("currency_efficiency_calculator [{RequestId}] no Elastic hit for {Term}.", requestId, search_term);
                return ApiResults.Fail("No currency matched the search term", StatusCodes.Status404NotFound);
            }

            var listings = await garland.GetItemTradeCurrencyAsync(currency.Id, ct);
            if (listings.Count == 0)
            {
                return ApiResults.Fail("Garland reports no tradeCurrency listings for this item", StatusCodes.Status404NotFound);
            }

            var marketableIds = (await structure.GetMarketableItemIdsAsync(ct)).ToHashSet();
            var byItemId = new Dictionary<int, GarlandTradeCurrencyListing>();
            foreach (var l in listings)
            {
                if (!marketableIds.Contains(l.ItemId)) continue;
                byItemId.TryAdd(l.ItemId, l);
            }

            if (byItemId.Count == 0)
            {
                return ApiResults.Fail("All trade-currency items are untradable", StatusCodes.Status404NotFound);
            }

            // Universalis caps multi-id lookups around 50.
            var mb = new Dictionary<int, UniversalisMarketBoardListing>();
            foreach (var chunk in byItemId.Keys.Chunk(50))
            {
                var partial = await universalis.GetMarketBoardDataAsync(location!, chunk, ct);
                foreach (var (id, l) in partial) mb[id] = l;
            }

            var itemNames = await structure.GetItemNamesAsync(ct);

            var raw = new List<RawCurrencyRow>(byItemId.Count);
            foreach (var (itemId, listing) in byItemId)
            {
                if (!mb.TryGetValue(itemId, out var market)) continue;

                var medianStack = MedianStackSize(market.StackSizeHistogram);
                var dailyMarketCap = (long)Math.Round(market.RegularSaleVelocity * medianStack * market.MinPrice);
                var rawScore = listing.CurrencyAmount > 0
                    ? (double)market.MinPrice * market.RegularSaleVelocity / listing.CurrencyAmount
                    : 0d;

                raw.Add(new RawCurrencyRow(
                    Id: itemId,
                    Name: itemNames.TryGetValue(itemId, out var name) ? name : string.Empty,
                    Price: listing.CurrencyAmount,
                    CurrencyId: listing.CurrencyId,
                    CurrencyName: itemNames.TryGetValue(listing.CurrencyId, out var currencyName) ? currencyName : string.Empty,
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

    // A null result means the query is usable; resolvedId falls back to a fresh random id.
    private static IResult? ValidateToolQuery(string? term, string? location, string? requestId, out string resolvedId)
    {
        resolvedId = string.Empty;

        if (string.IsNullOrWhiteSpace(term))
        {
            return ApiResults.Fail("GET request failed, please try again. Missing: search_term field", StatusCodes.Status400BadRequest);
        }
        if (string.IsNullOrWhiteSpace(location))
        {
            return ApiResults.Fail("GET request failed, please try again. Missing: location field", StatusCodes.Status400BadRequest);
        }

        resolvedId = string.IsNullOrWhiteSpace(requestId)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()
            : requestId;

        return null;
    }

    // Upper median: the stack size at index n/2 of the observations sorted ascending.
    internal static int MedianStackSize(IReadOnlyDictionary<int, int> histogram)
    {
        var buckets = histogram.Where(kv => kv.Value > 0).OrderBy(kv => kv.Key).ToList();
        var total = buckets.Sum(kv => (long)kv.Value);
        if (total == 0) return 0;

        var target = total / 2;
        var seen = 0L;
        foreach (var (size, occurrences) in buckets)
        {
            seen += occurrences;
            if (seen > target) return size;
        }

        return buckets[^1].Key;
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
