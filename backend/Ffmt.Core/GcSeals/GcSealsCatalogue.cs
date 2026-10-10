namespace Ffmt.Core.GcSeals;

public sealed record GcSealsCatalogue(
    IReadOnlyList<GcSealsItem> Items,
    IReadOnlyList<GcSealsRecipe> Recipes,
    IReadOnlyDictionary<int, string> Names);

public sealed record GcSealsItem(int Id, string Name, int ItemLevel, int Seals);

public sealed record GcSealsRecipe(int ItemId, int Yield, IReadOnlyList<GcSealsIngredient> Ingredients);

public sealed record GcSealsIngredient(int Id, int Amount);
