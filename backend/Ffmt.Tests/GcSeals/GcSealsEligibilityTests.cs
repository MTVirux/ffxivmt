using Ffmt.Core.External;
using Ffmt.Core.GcSeals;

namespace Ffmt.Tests.GcSeals;

public sealed class GcSealsEligibilityTests
{
    [Theory]
    [InlineData(2, 1, 2, true)]     // green main hand
    [InlineData(3, 3, 34, true)]    // blue head
    [InlineData(7, 4, 35, true)]    // aetherial body
    [InlineData(1, 1, 2, false)]    // white
    [InlineData(4, 1, 2, false)]    // relic
    [InlineData(2, 2, 11, true)]    // shield
    [InlineData(2, 2, 13, false)]   // crafter/gatherer secondary tool
    [InlineData(2, 6, 39, false)]   // belt
    [InlineData(2, 17, 62, false)]  // soul crystal
    [InlineData(2, 0, 0, false)]    // not equipment
    public void Follows_the_expert_delivery_rule(int rarity, int slot, int uiCategory, bool expected) =>
        GcSealsEligibility.IsEligible(new XivapiEquipmentCandidate(1, rarity, 100, slot, uiCategory)).Should().Be(expected);
}
