using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Ffmt.Core.Logging;

public static class LogChannelScope
{
    // Pushes the Serilog LogContext, which is what routes events to the channel's file sink.
    public static IDisposable Begin(string channel) =>
        LogContext.PushProperty(LogChannels.ContextPropertyName, channel);

    // Adds an MEL scope on top so the property is visible to non-Serilog consumers too.
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
