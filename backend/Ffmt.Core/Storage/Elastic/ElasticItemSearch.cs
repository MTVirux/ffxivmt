using System.Globalization;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using Ffmt.Core.Configuration;
using Ffmt.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ffmt.Core.Storage.Elastic;

public sealed class ElasticItemSearch : IElasticItemSearch
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticOptions _options;
    private readonly ILogger<ElasticItemSearch> _logger;

    public ElasticItemSearch(IOptions<ElasticOptions> options, ILogger<ElasticItemSearch> logger)
    {
        _options = options.Value;
        _logger = logger;

        var settings = new ElasticsearchClientSettings(new Uri(_options.Url))
            .DefaultIndex(_options.ItemsIndex)
            .RequestTimeout(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        if (!string.IsNullOrEmpty(_options.Username))
        {
            settings = settings.Authentication(new BasicAuthentication(_options.Username, _options.Password ?? string.Empty));
        }

        _client = new ElasticsearchClient(settings);
    }

    public async Task<IReadOnlyList<ElasticItemHit>> SearchByNameAsync(string query, int size, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<ItemDocument>(
            s => s
                .Indices(_options.ItemsIndex)
                .Size(size)
                .Query(q => q.Match(m => m.Field(f => f.Name).Query(query))),
            ct).ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Elasticsearch search failed: {Error}", response.DebugInformation);
            throw new ElasticsearchUnavailableException($"Elasticsearch search failed: {response.DebugInformation}");
        }

        var hits = new List<ElasticItemHit>(response.Hits.Count);
        foreach (var hit in response.Hits)
        {
            if (hit.Source is null)
            {
                continue;
            }

            var id = !string.IsNullOrEmpty(hit.Id) && int.TryParse(hit.Id, out var parsed)
                ? parsed
                : hit.Source.Id;

            hits.Add(new ElasticItemHit(id, hit.Source.Name ?? string.Empty, hit.Score));
        }
        return hits;
    }

    public async Task UpsertAsync(int id, string name, CancellationToken ct = default)
    {
        var doc = new ItemDocument { Id = id, Name = name };
        var response = await _client.IndexAsync(doc, idx => idx
            .Index(_options.ItemsIndex)
            .Id(DocumentId(id)), ct).ConfigureAwait(false);

        if (!response.IsValidResponse)
        {
            _logger.LogWarning("Elasticsearch upsert failed for item {ItemId}: {Error}", id, response.DebugInformation);
        }
    }

    public async Task UpsertManyAsync(IEnumerable<(int Id, string Name)> items, CancellationToken ct = default)
    {
        var docs = items.Select(i => new ItemDocument { Id = i.Id, Name = i.Name }).ToList();
        if (docs.Count == 0)
        {
            return;
        }

        var response = await _client.BulkAsync(b => b
            .Index(_options.ItemsIndex)
            .IndexMany(docs, (op, doc) => op.Id(DocumentId(doc.Id))), ct).ConfigureAwait(false);

        if (!response.IsValidResponse || response.Errors)
        {
            _logger.LogWarning("Elasticsearch bulk upsert of {Count} items reported errors: {Error}",
                docs.Count, response.DebugInformation);
        }
    }

    private static string DocumentId(int id) => id.ToString(CultureInfo.InvariantCulture);

    private sealed class ItemDocument
    {
        public int Id { get; init; }
        public string? Name { get; init; }
    }
}
