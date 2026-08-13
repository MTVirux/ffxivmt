namespace Ffmt.Core.Models;

/// <summary>One row in the <c>sales</c> Scylla table. No TTL - rows leave only via the archive prune.</summary>
public sealed record Sale(
    int ItemId,
    int WorldId,
    string BuyerName,
    bool Hq,
    bool OnMannequin,
    int Quantity,
    int UnitPrice,
    DateTimeOffset SaleTime);

public sealed record SaleBatchResult(int ParsedSales, double Time);
