using Ffmt.Core.Configuration;
using Ffmt.Core.External;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Status;

public static class StatusServiceCollectionExtensions
{
    public static IServiceCollection AddFfmtStatus(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PrometheusOptions>().Bind(configuration.GetSection(PrometheusOptions.SectionName)).ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);

        // Transient so it does not pin the typed Prometheus HttpClient; the cache lives in IMemoryCache.
        services.AddTransient<StatusMetricsService>();

        services.AddHttpClient<IPrometheusClient, PrometheusClient>(PrometheusClient.HttpClientName, (sp, http) =>
        {
            var opts = sp.GetRequiredService<IOptions<PrometheusOptions>>().Value;
            http.BaseAddress = new Uri(opts.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(opts.RequestTimeoutSeconds);
        });

        return services;
    }
}
