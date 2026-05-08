namespace WsWorker.Models;

public sealed class World
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Datacenter { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
}
