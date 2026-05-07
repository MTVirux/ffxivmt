using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Ffmt.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.HealthChecks;

public sealed class ElasticHealthCheck : IHealthCheck
{
    private readonly ElasticsearchClient _client;

    public ElasticHealthCheck(IOptions<ElasticOptions> options)
    {
        var opts = options.Value;
        var settings = new ElasticsearchClientSettings(new Uri(opts.Url))
            .RequestTimeout(TimeSpan.FromSeconds(Math.Min(opts.RequestTimeoutSeconds, 5)));

        if (!string.IsNullOrEmpty(opts.Username))
        {
            settings = settings.Authentication(new BasicAuthentication(opts.Username, opts.Password ?? string.Empty));
        }

        _client = new ElasticsearchClient(settings);
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var ping = await _client.PingAsync(cancellationToken).ConfigureAwait(false);
            return ping.IsValidResponse
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy(ping.DebugInformation);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
