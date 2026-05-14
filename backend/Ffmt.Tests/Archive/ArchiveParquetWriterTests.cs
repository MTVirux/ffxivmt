using Ffmt.Core.Archive;
using Ffmt.Core.Models;
using Parquet;
using Parquet.Data;

namespace Ffmt.Tests.Archive;

public sealed class ArchiveParquetWriterTests
{
    [Fact]
    public async Task WriteAsync_SortsByItemIdAscending()
    {
        var sales = new List<Sale>
        {
            new(ItemId: 5, WorldId: 21, BuyerName: "Alisaie", Hq: true, OnMannequin: false,
                Quantity: 2, UnitPrice: 500, SaleTime: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
            new(ItemId: 2, WorldId: 21, BuyerName: "Alphinaud", Hq: false, OnMannequin: false,
                Quantity: 1, UnitPrice: 100, SaleTime: new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero)),
        };

        var bytes = await ArchiveParquetWriter.WriteAsync(sales);

        using var ms = new MemoryStream(bytes);
        using var reader = await ParquetReader.CreateAsync(ms);
        reader.RowGroupCount.Should().Be(1);

        using var rg = reader.OpenRowGroupReader(0);
        var fields = reader.Schema.DataFields;
        var itemIdCol = await rg.ReadColumnAsync(fields.First(f => f.Name == "item_id"));
        ((int[])itemIdCol.Data).Should().Equal(2, 5);
    }

    [Fact]
    public async Task WriteAsync_EmptyInput_ProducesValidEmptyParquet()
    {
        var bytes = await ArchiveParquetWriter.WriteAsync([]);

        bytes.Should().NotBeEmpty();
        using var ms = new MemoryStream(bytes);
        using var reader = await ParquetReader.CreateAsync(ms);
        reader.RowGroupCount.Should().Be(0);
    }

    [Fact]
    public async Task ReadAsync_RoundTripsWriteAsync()
    {
        var sales = new List<Sale>
        {
            new(ItemId: 2, WorldId: 21, BuyerName: "Alphinaud", Hq: false, OnMannequin: false,
                Quantity: 1, UnitPrice: 100, SaleTime: new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero)),
            new(ItemId: 5, WorldId: 21, BuyerName: "Alisaie", Hq: true, OnMannequin: false,
                Quantity: 2, UnitPrice: 500, SaleTime: new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero)),
        };

        var bytes = await ArchiveParquetWriter.WriteAsync(sales);
        var result = await ArchiveParquetWriter.ReadAsync(bytes);

        result.Should().HaveCount(2);
        result[0].ItemId.Should().Be(2);
        result[0].WorldId.Should().Be(21);
        result[0].BuyerName.Should().Be("Alphinaud");
        result[0].Hq.Should().BeFalse();
        result[0].Quantity.Should().Be(1);
        result[0].UnitPrice.Should().Be(100);
        result[0].SaleTime.Should().Be(new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.Zero));
        result[1].ItemId.Should().Be(5);
        result[1].BuyerName.Should().Be("Alisaie");
        result[1].Hq.Should().BeTrue();
    }
}
