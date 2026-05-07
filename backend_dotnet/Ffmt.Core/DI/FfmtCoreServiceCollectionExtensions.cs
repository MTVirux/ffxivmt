using Ffmt.Core.Configuration;
using Ffmt.Core.Gilflux;
using Ffmt.Core.HealthChecks;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ffmt.Core.DI;

public static class FfmtCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers configuration bindings, storage clients, and shared services consumed by both
    /// <c>Ffmt.Api</c> and <c>Ffmt.Cli</c>. Health-check pipeline wiring is the API host's responsibility.
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

        // Storage
        services.AddSingleton<IScyllaSession, ScyllaSession>();
        services.AddSingleton<IItemStore, ScyllaItemStore>();
        services.AddSingleton<IWorldStore, ScyllaWorldStore>();
        services.AddSingleton<IGilfluxRankingStore, ScyllaGilfluxRankingStore>();
        services.AddSingleton<IElasticItemSearch, ElasticItemSearch>();

        // Domain
        services.AddSingleton<WorldStructureService>();
        services.AddSingleton<LocationResolver>();
        services.AddSingleton<GilfluxRankingReader>();

        // Health checks (registered as services here; the API host adds them to the pipeline).
        services.AddSingleton<ScyllaHealthCheck>();
        services.AddSingleton<ElasticHealthCheck>();

        return services;
    }
}
