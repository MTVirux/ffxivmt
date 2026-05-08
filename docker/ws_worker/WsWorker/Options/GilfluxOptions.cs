namespace WsWorker.Options;

public sealed class GilfluxOptions
{
    public double CoalesceWindowSeconds { get; set; } = 2.0;
    public int Workers { get; set; } = 8;
    public int QueueMax { get; set; } = 1000;
    public double HttpTimeoutSeconds { get; set; } = 10.0;
}
