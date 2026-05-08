using Ffmt.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Ffmt.Core.Logging;

public static class SerilogBootstrap
{
    public static LoggerConfiguration Configure(LoggerConfiguration logger, IConfiguration configuration, string environmentName)
    {
        var options = configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>() ?? new LoggingOptions();
        var enabled = new HashSet<string>(options.ChannelsEnabled, StringComparer.OrdinalIgnoreCase);

        Directory.CreateDirectory(options.LogDirectory);

        logger
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Environment", environmentName)
            .WriteTo.Console(new CompactJsonFormatter());

        foreach (var channel in enabled)
        {
            var path = Path.Combine(options.LogDirectory, $"{channel}.log");
            logger.WriteTo.Logger(sub => sub
                .Filter.ByIncludingOnly(e =>
                    e.Properties.TryGetValue(LogChannels.ContextPropertyName, out var prop)
                    && prop is Serilog.Events.ScalarValue { Value: string s }
                    && string.Equals(s, channel, StringComparison.OrdinalIgnoreCase))
                .WriteTo.File(
                    new CompactJsonFormatter(),
                    path,
                    rollingInterval: RollingInterval.Day,
                    fileSizeLimitBytes: options.FileRollingSizeBytes,
                    retainedFileCountLimit: options.FileRetainedFileCount,
                    rollOnFileSizeLimit: true));
        }

        if (options.MirrorToAllLog && !string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            logger.WriteTo.File(
                new CompactJsonFormatter(),
                Path.Combine(options.LogDirectory, "ALL.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: options.FileRollingSizeBytes,
                retainedFileCountLimit: options.FileRetainedFileCount,
                rollOnFileSizeLimit: true);
        }

        return logger;
    }
}
