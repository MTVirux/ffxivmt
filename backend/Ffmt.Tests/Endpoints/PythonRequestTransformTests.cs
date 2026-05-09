using Ffmt.Api.Endpoints;
using Ffmt.Core.Models;

namespace Ffmt.Tests.Endpoints;

public sealed class PythonRequestTransformTests
{
    private static readonly Dictionary<int, World> Worlds = new()
    {
        [21] = new World(21, "Ravana", "Light", "Europe"),
        [42] = new World(42, "Phoenix", "Light", "Europe"),
    };

    private static readonly Dictionary<int, string> ItemNames = new()
    {
        [12345] = "Mythril Ingot",
        [67890] = "Eblan Cotton Boll",
    };

    [Fact]
    public void EmptyPayload_ProducesNoSales()
    {
        var payload = new PythonRequestPayload { WorldId = 21, Items = null };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().BeEmpty();
        result.RankingPairs.Should().BeEmpty();
    }

    [Fact]
    public void SingleEntry_PopulatesCanonicalFields()
    {
        var payload = new PythonRequestPayload
        {
            WorldId = 21,
            Items = new()
            {
                ["12345"] = new()
                {
                    Entries =
                    [
                        new()
                        {
                            BuyerName = "Alice",
                            Hq = 1,
                            OnMannequin = false,
                            PricePerUnit = 100,
                            Quantity = 5,
                            Timestamp = 1_700_000_000,
                        },
                    ],
                },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().HaveCount(1);
        var sale = result.Sales[0];
        sale.ItemId.Should().Be(12345);
        sale.WorldId.Should().Be(21);
        sale.BuyerName.Should().Be("Alice");
        sale.Hq.Should().BeTrue();
        sale.UnitPrice.Should().Be(100);
        sale.Quantity.Should().Be(5);
        sale.SaleTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        result.RankingPairs.Should().BeEquivalentTo([(WorldId: 21, ItemId: 12345)]);
    }

    [Fact]
    public void Hq_NonOne_BindsAsFalse()
    {
        var payload = new PythonRequestPayload
        {
            WorldId = 21,
            Items = new()
            {
                ["12345"] = new() { Entries = [new() { Hq = 0, Timestamp = 1, Quantity = 1, PricePerUnit = 1 }] },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().ContainSingle().Which.Hq.Should().BeFalse();
    }

    [Fact]
    public void PerEntryWorldId_OverridesTopLevel()
    {
        var payload = new PythonRequestPayload
        {
            WorldId = 21,
            Items = new()
            {
                ["12345"] = new()
                {
                    Entries =
                    [
                        new() { WorldId = 42, Quantity = 1, PricePerUnit = 1, Timestamp = 1 },
                    ],
                },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().ContainSingle().Which.WorldId.Should().Be(42);
        result.RankingPairs.Should().BeEquivalentTo([(WorldId: 42, ItemId: 12345)]);
    }

    [Fact]
    public void EntryWithUnknownWorld_IsSkipped()
    {
        var payload = new PythonRequestPayload
        {
            Items = new()
            {
                ["12345"] = new() { Entries = [new() { WorldId = 999, Quantity = 1, PricePerUnit = 1, Timestamp = 1 }] },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().BeEmpty();
        result.RankingPairs.Should().BeEmpty();
    }

    [Fact]
    public void EntryWithoutAnyWorldId_IsSkipped()
    {
        var payload = new PythonRequestPayload
        {
            Items = new()
            {
                ["12345"] = new() { Entries = [new() { Quantity = 1, PricePerUnit = 1, Timestamp = 1 }] },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().BeEmpty();
    }

    [Fact]
    public void DuplicatePairs_CollapseInRankingSet()
    {
        var payload = new PythonRequestPayload
        {
            WorldId = 21,
            Items = new()
            {
                ["12345"] = new()
                {
                    Entries =
                    [
                        new() { Quantity = 1, PricePerUnit = 1, Timestamp = 1 },
                        new() { Quantity = 2, PricePerUnit = 3, Timestamp = 2 },
                    ],
                },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().HaveCount(2);
        result.RankingPairs.Should().HaveCount(1);
        result.RankingPairs.Should().BeEquivalentTo([(WorldId: 21, ItemId: 12345)]);
    }

    [Fact]
    public void NonNumericItemKey_IsSkipped()
    {
        var payload = new PythonRequestPayload
        {
            WorldId = 21,
            Items = new()
            {
                ["not_an_int"] = new() { Entries = [new() { Quantity = 1, PricePerUnit = 1, Timestamp = 1 }] },
            },
        };

        var result = PythonRequestTransform.Build(payload, Worlds, ItemNames);

        result.Sales.Should().BeEmpty();
    }
}
