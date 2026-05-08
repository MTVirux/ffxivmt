namespace WsWorker.Options;

public sealed class UniversalisOptions
{
    public string WsUrl { get; set; } = "wss://universalis.app/api/ws";
    public string ApiUrl { get; set; } = "https://universalis.app/api/v2/";
    public string[] RegionsToUse { get; set; } = ["Europe", "North-America"];
    public string[] RegionsToImport { get; set; } = ["Europe"];
    public int ItemsPerRequest { get; set; } = 100;
    public int MaxRequestsPerSecond { get; set; } = 25;
    public int MaxRequestsPerSecondBurst { get; set; } = 50;
}
