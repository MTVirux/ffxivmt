using System.IO.Compression;
using Ffmt.Core.Models;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace Ffmt.Core.Archive;

public static class ArchiveParquetWriter
{
    private static readonly ParquetSchema Schema = new(
        new DataField<int>("item_id"),
        new DataField<long>("sale_time"),
        new DataField<int>("world_id"),
        new DataField<bool>("hq"),
        new DataField<bool>("on_mannequin"),
        new DataField<int>("quantity"),
        new DataField<int>("unit_price"),
        new DataField<string>("buyer_name")
    );

    public static async Task<byte[]> WriteAsync(IReadOnlyList<Sale> sales)
    {
        var sorted = sales
            .OrderBy(s => s.ItemId)
            .ThenBy(s => s.SaleTime)
            .ToList();

        using var ms = new MemoryStream();

        using (var writer = await ParquetWriter.CreateAsync(Schema, ms))
        {
            writer.CompressionMethod = CompressionMethod.Zstd;
            writer.CompressionLevel = CompressionLevel.Optimal;

            if (sorted.Count > 0)
            {
                using var rg = writer.CreateRowGroup();
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[0], sorted.Select(s => s.ItemId).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[1], sorted.Select(s => s.SaleTime.ToUnixTimeSeconds()).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[2], sorted.Select(s => s.WorldId).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[3], sorted.Select(s => s.Hq).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[4], sorted.Select(s => s.OnMannequin).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[5], sorted.Select(s => s.Quantity).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[6], sorted.Select(s => s.UnitPrice).ToArray()));
                await rg.WriteColumnAsync(new DataColumn(Schema.DataFields[7], sorted.Select(s => s.BuyerName).ToArray()));
            }
        }

        return ms.ToArray();
    }

    public static List<Sale> Merge(IReadOnlyList<Sale> existing, IReadOnlyList<Sale> incoming)
    {
        var seen = new HashSet<(int, int, DateTimeOffset)>(
            existing.Select(s => (s.ItemId, s.WorldId, s.SaleTime)));
        var result = new List<Sale>(existing);
        foreach (var s in incoming)
        {
            if (seen.Add((s.ItemId, s.WorldId, s.SaleTime)))
                result.Add(s);
        }
        return result;
    }

    public static async Task<List<Sale>> ReadAsync(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var reader = await ParquetReader.CreateAsync(ms);

        var result = new List<Sale>();
        for (var i = 0; i < reader.RowGroupCount; i++)
        {
            using var rg = reader.OpenRowGroupReader(i);
            var fields = reader.Schema.DataFields;
            var itemIds    = (int[])   (await rg.ReadColumnAsync(fields.First(f => f.Name == "item_id"))).Data;
            var saleTimes  = (long[])  (await rg.ReadColumnAsync(fields.First(f => f.Name == "sale_time"))).Data;
            var worldIds   = (int[])   (await rg.ReadColumnAsync(fields.First(f => f.Name == "world_id"))).Data;
            var hqs        = (bool[])  (await rg.ReadColumnAsync(fields.First(f => f.Name == "hq"))).Data;
            var mannequins = (bool[])  (await rg.ReadColumnAsync(fields.First(f => f.Name == "on_mannequin"))).Data;
            var quantities = (int[])   (await rg.ReadColumnAsync(fields.First(f => f.Name == "quantity"))).Data;
            var prices     = (int[])   (await rg.ReadColumnAsync(fields.First(f => f.Name == "unit_price"))).Data;
            var buyers     = (string[])(await rg.ReadColumnAsync(fields.First(f => f.Name == "buyer_name"))).Data;

            for (var j = 0; j < itemIds.Length; j++)
            {
                result.Add(new Sale(
                    ItemId:      itemIds[j],
                    WorldId:     worldIds[j],
                    BuyerName:   buyers[j] ?? string.Empty,
                    Hq:          hqs[j],
                    OnMannequin: mannequins[j],
                    Quantity:    quantities[j],
                    UnitPrice:   prices[j],
                    SaleTime:    DateTimeOffset.FromUnixTimeSeconds(saleTimes[j])));
            }
        }
        return result;
    }
}
