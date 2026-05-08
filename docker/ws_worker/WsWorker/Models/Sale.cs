namespace WsWorker.Models;

public sealed class Sale
{
    public string BuyerName { get; set; } = string.Empty;
    public bool Hq { get; set; }
    public bool OnMannequin { get; set; }
    public int UnitPrice { get; set; }
    public int Quantity { get; set; }
    /// <summary>Unix timestamp in milliseconds.</summary>
    public long SaleTime { get; set; }
    public int WorldId { get; set; }
    public int ItemId { get; set; }
    public string WorldName { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Datacenter { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int Total { get; set; }
}
