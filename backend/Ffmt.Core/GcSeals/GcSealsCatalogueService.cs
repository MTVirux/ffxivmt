using Ffmt.Core.External;
using Ffmt.Core.Logging;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Ffmt.Core.GcSeals;

public sealed class GcSealsCatalogueService(
    IXivapiClient xivapi,
    WorldStructureService structure,
    IMemoryCache cache,
    ILogger<GcSealsCatalogueService> logger)
{
    internal const string CacheKey = "ffmt:gcseals:catalogue";
    private const string LastGoodKey = "ffmt:gcseals:catalogue:lastGood";
    private const int MaxTreeDepth = 8;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);

    // Static because the service is transient, so it never pins the typed XIVAPI HttpClient.
    private static readonly SemaphoreSlim BuildLock = new(1, 1);

    public async Task<GcSealsCatalogue> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out GcSealsCatalogue? cached) && cached is not null)
        {
            return cached;
        }

        await BuildLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (cache.TryGetValue(CacheKey, out cached) && cached is not null)
            {
                return cached;
            }

            GcSealsCatalogue catalogue;
            try
            {
                catalogue = await BuildAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested
                && cache.TryGetValue(LastGoodKey, out GcSealsCatalogue? lastGood) && lastGood is not null)
            {
                using var _ = LogChannelScope.Begin(logger, LogChannels.ApiError);
                logger.LogWarning(ex, "GC seals catalogue rebuild failed; serving the previous copy.");
                cache.Set(CacheKey, lastGood, RetryAfterFailure);
                return lastGood;
            }

            cache.Set(LastGoodKey, catalogue, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            cache.Set(CacheKey, catalogue, CacheTtl);
            return catalogue;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    private async Task<GcSealsCatalogue> BuildAsync(CancellationToken ct)
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.ApiInfo);

        var candidatesTask = xivapi.GetEquipmentCandidatesAsync(ct);
        var recipesTask = xivapi.GetAllRecipesAsync(ct);
        var sealsTask = xivapi.GetExpertDeliverySealsAsync(ct);
        var marketableTask = structure.GetMarketableItemIdsAsync(ct);
        var namesTask = structure.GetItemNamesAsync(ct);
        await Task.WhenAll(candidatesTask, recipesTask, sealsTask, marketableTask, namesTask).ConfigureAwait(false);

        var seals = await sealsTask.ConfigureAwait(false);
        var names = await namesTask.ConfigureAwait(false);
        var marketable = (await marketableTask.ConfigureAwait(false)).ToHashSet();
        var recipesByResult = (await recipesTask.ConfigureAwait(false)).ToLookup(r => r.ResultItemId);

        var items = (await candidatesTask.ConfigureAwait(false))
            .Where(GcSealsEligibility.IsEligible)
            .Where(c => marketable.Contains(c.ItemId) && recipesByResult.Contains(c.ItemId))
            .DistinctBy(c => c.ItemId)
            .Select(c => new GcSealsItem(c.ItemId, NameOf(names, c.ItemId), c.ItemLevel, seals.GetValueOrDefault(c.ItemLevel)))
            .Where(i => i.Seals > 0)
            .OrderBy(i => i.Id)
            .ToList();

        var recipes = new List<GcSealsRecipe>();
        var visited = new HashSet<int>();
        var frontier = items.Select(i => i.Id).ToList();
        for (var depth = 0; depth < MaxTreeDepth && frontier.Count > 0; depth++)
        {
            var next = new List<int>();
            foreach (var id in frontier)
            {
                if (!visited.Add(id))
                {
                    continue;
                }

                foreach (var recipe in recipesByResult[id])
                {
                    recipes.Add(new GcSealsRecipe(
                        id, recipe.Yield, recipe.Ingredients.Select(x => new GcSealsIngredient(x.ItemId, x.Amount)).ToList()));
                    next.AddRange(recipe.Ingredients.Select(x => x.ItemId));
                }
            }
            frontier = next;
        }

        var referenced = items.Select(i => i.Id)
            .Concat(recipes.SelectMany(r => r.Ingredients.Select(x => x.Id).Append(r.ItemId)))
            .Distinct()
            .Order();
        var catalogueNames = referenced.ToDictionary(id => id, id => NameOf(names, id));

        logger.LogInformation("GC seals catalogue: {Items} items, {Recipes} recipes, {Ids} ids.",
            items.Count, recipes.Count, catalogueNames.Count);
        return new GcSealsCatalogue(items, recipes, catalogueNames);
    }

    private static string NameOf(IReadOnlyDictionary<int, string> names, int id) =>
        names.TryGetValue(id, out var name) ? name : string.Empty;
}
