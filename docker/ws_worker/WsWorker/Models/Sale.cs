namespace WsWorker.Models;

public sealed class Sale
{
    public string BuyerName { get; init; } = string.Empty;
    public bool Hq { get; init; }
    public bool OnMannequin { get; init; }
    public int UnitPrice { get; init; }
    public int Quantity { get; init; }
    public long SaleTime { get; init; }   // unix ms
    public int WorldId { get; init; }
    public int ItemId { get; init; }
}
