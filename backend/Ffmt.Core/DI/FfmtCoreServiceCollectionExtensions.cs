using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Ffmt.Core.Gilflux;
using Ffmt.Core.HealthChecks;
using Ffmt.Core.Storage.Elastic;
using Ffmt.Core.Storage.Scylla;
using Ffmt.Core.Worlds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.DI;

public static class FfmtCoreServiceCollectionExtensions
{
    public static IServiceCollection AddFfmtCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ScyllaOptions>().Bind(configuration.GetSection(ScyllaOptions.SectionName)).ValidateOnStart();
        services.AddOptions<ElasticOptions>().Bind(configuration.GetSection(ElasticOptions.SectionName)).ValidateOnStart();
        services.AddOptions<UniversalisOptions>().Bind(configuration.GetSection(UniversalisOptions.SectionName)).ValidateOnStart();
        services.AddOptions<GarlandOptions>().Bind(configuration.GetSection(GarlandOptions.SectionName)).ValidateOnStart();
        services.AddOptions<GilfluxOptions>().Bind(configuration.GetSection(GilfluxOptions.SectionName)).ValidateOnStart();
        services.AddOptions<LoggingOptions>().Bind(configuration.GetSection(LoggingOptions.SectionName)).ValidateOnStart();
        services.AddOptions<UpdatedbOptions>().Bind(configuration.GetSection(UpdatedbOptions.SectionName)).ValidateOnStart();

        services.AddMemoryCache();

        services.AddSingleton<IScyllaSession, ScyllaSession>();
        services.AddSingleton<IItemStore, ScyllaItemStore>();
        services.AddSingleton<IWorldStore, ScyllaWorldStore>();
        services.AddSingleton<IGilfluxRankingStore, ScyllaGilfluxRankingStore>();
        services.AddSingleton<IRankingRefresher, ScyllaRankingRefresher>();
        services.AddSingleton<IDirtyPairQueue, ScyllaDirtyPairQueue>();
        services.AddSingleton<ISaleStore, ScyllaSaleStore>();
        services.AddSingleton<IElasticItemSearch, ElasticItemSearch>();

        services.AddSingleton<WorldStructureService>();
        services.AddSingleton<LocationResolver>();
        services.AddSingleton<GilfluxRankingReader>();

        services.AddHttpClient<IUniversalisClient, UniversalisClient>(UniversalisClient.HttpClientName, (sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<UniversalisOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<UniversalisOptions>>().Value;
                return HttpRetryPolicy.Build(opts.MaxRetries, opts.InitialBackoffSeconds, opts.MaxBackoffSeconds);
            });

        services.AddHttpClient<IGarlandClient, GarlandClient>(GarlandClient.HttpClientName, (sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<GarlandOptions>>().Value;
                http.BaseAddress = new Uri(opts.BaseUrl);
                http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
            })
            .AddPolicyHandler((sp, _) =>
            {
                var opts = sp.GetRequiredService<IOptions<GarlandOptions>>().Value;
                return HttpRetryPolicy.Build(opts.MaxRetries, opts.InitialBackoffSeconds, opts.MaxBackoffSeconds);
            });

        services.AddSingleton<ScyllaHealthCheck>();
        services.AddSingleton<ElasticHealthCheck>();

        return services;
    }
}
