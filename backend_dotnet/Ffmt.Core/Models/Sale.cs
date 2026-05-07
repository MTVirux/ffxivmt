namespace Ffmt.Core.Models;

/// <summary>
/// One row in the <c>sales</c> Scylla table. The schema lives in
/// <c>docker/scylla/startup_scripts/2- create_sales_table.sh</c>; rows TTL out at 8 days.
/// </summary>
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

/// <summary>Result of a <see cref="Storage.Scylla.ISaleStore.AddBatchAsync"/> call.</summary>
public sealed record SaleBatchResult(int ParsedSales, double Time);
