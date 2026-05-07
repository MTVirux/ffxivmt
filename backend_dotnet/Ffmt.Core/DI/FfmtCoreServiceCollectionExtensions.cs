using Ffmt.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ffmt.Core.DI;

public static class FfmtCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers configuration bindings and shared services consumed by both <c>Ffmt.Api</c> and <c>Ffmt.Cli</c>.
    /// Storage clients (Scylla, Elastic) and HTTP clients (Universalis, Garland) are wired in later phases as their stores land.
    /// </summary>
    public static IServiceCollection AddFfmtCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ScyllaOptions>().Bind(configuration.GetSection(ScyllaOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ElasticOptions>().Bind(configuration.GetSection(ElasticOptions.SectionName)).ValidateOnStart();
        services.AddOptions<UniversalisOptions>().Bind(configuration.GetSection(UniversalisOptions.SectionName)).ValidateOnStart();
        services.AddOptions<GarlandOptions>().Bind(configuration.GetSection(GarlandOptions.SectionName)).ValidateOnStart();
        services.AddOptions<GilfluxOptions>().Bind(configuration.GetSection(GilfluxOptions.SectionName)).ValidateOnStart();
        services.AddOptions<LoggingOptions>().Bind(configuration.GetSection(LoggingOptions.SectionName)).ValidateOnStart();

        services.AddMemoryCache();

        return services;
    }
}
