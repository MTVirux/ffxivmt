namespace Ffmt.Core.Models;

/// <summary><c>craftable</c> / <c>marketable</c> / <c>from_scrips</c> are seeded <c>false</c> and flipped by later stages.</summary>
public sealed record ItemUpsert(
    int Id,
    string Name,
    string Description,
    bool CanBeHq,
    bool AlwaysCollectible,
    int StackSize,
    int ItemLevel,
    int IconImage,
    int Rarity,
    int FilterGroup,
    int ItemUiCategory,
    int ItemSearchCategory,
    int EquipSlotCategory,
    bool Unique,
    bool Untradable,
    bool Disposable,
    bool Dyable,
    bool AetherialReductible,
    int MateriaSlotCount,
    bool AdvancedMelding);
