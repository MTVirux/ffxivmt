namespace Ffmt.Core.Models;

/// <summary>One row in the <c>sales</c> Scylla table; rows TTL out at 8 days.</summary>
public sealed record Sale(
    int ItemId,
    int WorldId,
    string ItemName,
    string WorldName,
    string Datacenter,
    string Region,
    string BuyerName,
    bool Hq,
    bool OnMannequin,
    int Quantity,
    int UnitPrice,
    int Total,
    DateTimeOffset SaleTime);

public sealed record SaleBatchResult(int ParsedSales, double Time);
