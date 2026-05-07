namespace Ffmt.Core.Models;

/// <summary>
/// Full-row payload used by the <c>updatedb</c> CLI when seeding the <c>items</c> table from
/// the FFXIV datamining CSV. Columns map 1:1 onto the Scylla schema in
/// <c>docker/scylla/startup_scripts/3- create_items_table.sh</c>; <c>craftable</c> /
/// <c>marketable</c> / <c>from_scrips</c> are seeded <c>false</c> here and flipped by later
/// stages (Garland for craftable, Universalis for marketable).
/// </summary>
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
