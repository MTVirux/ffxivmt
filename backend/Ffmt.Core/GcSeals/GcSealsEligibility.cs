using Ffmt.Core.External;

namespace Ffmt.Core.GcSeals;

/// <summary>Allagan Tools' Expert Delivery rule. Crafter and gatherer secondary tools share the
/// off-hand slot with shields; only shields are accepted.</summary>
public static class GcSealsEligibility
{
    private const int OffHandSlot = 2;
    private const int BeltSlot = 6;
    private const int SoulCrystalSlot = 17;
    private const int ShieldUiCategory = 11;

    public static bool IsEligible(XivapiEquipmentCandidate c) =>
        c.Rarity is 2 or 3 or 7
        && c.EquipSlotCategory > 0
        && c.EquipSlotCategory is not (BeltSlot or SoulCrystalSlot)
        && (c.EquipSlotCategory != OffHandSlot || c.ItemUiCategory == ShieldUiCategory);
}
