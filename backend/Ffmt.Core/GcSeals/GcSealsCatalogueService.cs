using System.Diagnostics.CodeAnalysis;
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
    private const string BuildKey = "ffmt:gcseals:catalogue:build";
    private const int MaxTreeDepth = 8;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);
    private static readonly MemoryCacheEntryOptions Pinned = new() { Priority = CacheItemPriority.NeverRemove };

    // Static because the service is transient; a per-instance lock would let two requests start two builds.
    private static readonly Lock StartLock = new();

    public Task<GcSealsCatalogue> GetAsync(CancellationToken ct = default)
    {
        if (TryGet(CacheKey, out var cached))
        {
            return Task.FromResult(cached);
        }

        // One build is shared by every caller and never sees a caller's token, so a client that
        // disconnects cannot throw the XIVAPI walk away. Callers get the last good copy while it runs.
        var build = GetOrStartBuild().Value;
        return TryGet(LastGoodKey, out var lastGood) ? Task.FromResult(lastGood) : build.WaitAsync(ct);
    }

    private Lazy<Task<GcSealsCatalogue>> GetOrStartBuild()
    {
        lock (StartLock)
        {
            if (!cache.TryGetValue(BuildKey, out Lazy<Task<GcSealsCatalogue>>? build) || build is null)
            {
                build = new Lazy<Task<GcSealsCatalogue>>(StartBuild);
                cache.Set(BuildKey, build, Pinned);
            }
            return build;
        }
    }

    private Task<GcSealsCatalogue> StartBuild()
    {
        var build = RefreshAsync();
        // A failed build whose waiters all cancelled would otherwise raise UnobservedTaskException.
        _ = build.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return build;
    }

    private async Task<GcSealsCatalogue> RefreshAsync()
    {
        try
        {
            var catalogue = await BuildAsync().ConfigureAwait(false);
            cache.Set(LastGoodKey, catalogue, Pinned);
            cache.Set(CacheKey, catalogue, CacheTtl);
            return catalogue;
        }
        catch (Exception ex)
        {
            using var _ = LogChannelScope.Begin(logger, LogChannels.ApiError);
            if (!TryGet(LastGoodKey, out var lastGood))
            {
                logger.LogError(ex, "GC seals catalogue build failed.");
                throw;
            }

            logger.LogWarning(ex, "GC seals catalogue rebuild failed; serving the previous copy.");
            cache.Set(CacheKey, lastGood, RetryAfterFailure);
            return lastGood;
        }
        finally
        {
            lock (StartLock)
            {
                cache.Remove(BuildKey);
            }
        }
    }

    private bool TryGet(string key, [NotNullWhen(true)] out GcSealsCatalogue? catalogue) =>
        cache.TryGetValue(key, out catalogue) && catalogue is not null;

    private async Task<GcSealsCatalogue> BuildAsync()
    {
        using var _ = LogChannelScope.Begin(logger, LogChannels.ApiInfo);

        var candidatesTask = xivapi.GetEquipmentCandidatesAsync();
        var recipesTask = xivapi.GetAllRecipesAsync();
        var sealsTask = xivapi.GetExpertDeliverySealsAsync();
        var marketableTask = structure.GetMarketableItemIdsAsync();
        var namesTask = structure.GetItemNamesAsync();
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
