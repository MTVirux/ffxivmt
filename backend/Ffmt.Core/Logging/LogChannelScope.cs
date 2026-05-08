using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Ffmt.Core.Logging;

public static class LogChannelScope
{
    /// <summary>Routes events emitted inside the scope to the matching per-channel file sink.</summary>
    public static IDisposable Begin(string channel) =>
        LogContext.PushProperty(LogChannels.ContextPropertyName, channel);

    /// <summary>Also opens an MEL scope so structured properties are visible to non-Serilog consumers.</summary>
    public static IDisposable Begin(ILogger logger, string channel)
    {
        var serilog = Begin(channel);
        var mel = logger.BeginScope(new Dictionary<string, object> { [LogChannels.ContextPropertyName] = channel });
        return new Composite(serilog, mel);
    }

    private sealed class Composite(IDisposable a, IDisposable? b) : IDisposable
    {
        public void Dispose()
        {
            b?.Dispose();
            a.Dispose();
        }
    }
}
