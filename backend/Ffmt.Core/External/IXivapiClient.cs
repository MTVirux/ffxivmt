namespace Ffmt.Core.External;

public interface IXivapiClient
{
    /// <summary>Recipes whose result is green-or-better equipment with a vendor price. One entry per
    /// recipe, so an item with several recipes appears more than once.</summary>
    Task<IReadOnlyList<XivapiEquipmentCandidate>> GetEquipmentCandidatesAsync(CancellationToken ct = default);

    Task<IReadOnlyList<XivapiRecipe>> GetAllRecipesAsync(CancellationToken ct = default);

    /// <summary>Expert Delivery seals keyed by item level; levels worth 0 seals are left out.</summary>
    Task<IReadOnlyDictionary<int, int>> GetExpertDeliverySealsAsync(CancellationToken ct = default);
}

public sealed record XivapiEquipmentCandidate(int ItemId, int Rarity, int ItemLevel, int EquipSlotCategory, int ItemUiCategory);

public sealed record XivapiIngredient(int ItemId, int Amount);

public sealed record XivapiRecipe(int ResultItemId, int Yield, IReadOnlyList<XivapiIngredient> Ingredients);
