using Ffmt.Core.External;

namespace Ffmt.Tests.Workers;

public sealed class HistoryResponseParsingTests
{
    // The shape Universalis actually returns for a multi-item request: "items" keyed by item id.
    private const string MultiItemJson = """
        {
          "itemIDs": [2, 3],
          "items": {
            "2": { "itemID": 2, "entries": [
              { "hq": false, "pricePerUnit": 65, "quantity": 1500, "buyerName": "Ipomoea Albas",
                "onMannequin": false, "timestamp": 1786036726, "worldID": 67 }
            ]},
            "3": { "itemID": 3, "entries": [
              { "hq": true, "pricePerUnit": 48, "quantity": 500, "buyerName": "Bichi Suke",
                "onMannequin": false, "timestamp": 1786036000, "worldID": 21 }
            ]}
          },
          "unresolvedItems": []
        }
        """;

    private const string SingleItemJson = """
        {
          "itemID": 2,
          "entries": [
            { "hq": false, "pricePerUnit": 47, "quantity": 100, "buyerName": "Solo Buyer",
              "onMannequin": false, "timestamp": 1785937670, "worldID": 67 }
          ]
        }
        """;

    [Fact]
    public void Parses_the_multi_item_object_shape()
    {
        var sales = UniversalisHistoryParser.Parse(MultiItemJson);

        sales.Should().HaveCount(2,
            "an object-keyed items map is what a multi-item request returns - treating it as an array " +
            "silently imports nothing and makes the crawl look complete");
        sales.Select(s => s.ItemId).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void Maps_every_field_off_a_multi_item_entry()
    {
        var sale = UniversalisHistoryParser.Parse(MultiItemJson).Single(s => s.ItemId == 3);

        sale.WorldId.Should().Be(21);
        sale.Hq.Should().BeTrue();
        sale.Quantity.Should().Be(500);
        sale.UnitPrice.Should().Be(48);
        sale.BuyerName.Should().Be("Bichi Suke");
        sale.SaleTime.Should().Be(DateTimeOffset.FromUnixTimeSeconds(1786036000));
    }

    [Fact]
    public void Still_parses_the_single_item_root_entries_shape()
    {
        var sales = UniversalisHistoryParser.Parse(SingleItemJson);

        sales.Should().ContainSingle();
        sales[0].ItemId.Should().Be(2);
        sales[0].UnitPrice.Should().Be(47);
    }

    [Fact]
    public void An_empty_items_map_yields_no_sales()
    {
        UniversalisHistoryParser.Parse("""{"itemIDs":[],"items":{},"unresolvedItems":[]}""")
            .Should().BeEmpty();
    }
}
