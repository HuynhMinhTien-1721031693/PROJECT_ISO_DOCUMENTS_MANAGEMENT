using Elastic.Clients.Elasticsearch;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Search;

public sealed class ElasticsearchService : ISearchService
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchService> _logger;

    public ElasticsearchService(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchService> logger)
    {
        _client = client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task IndexDocumentAsync(Document document, CancellationToken ct = default)
    {
        var indexDoc = MapToIndexDocument(document);
        var response = await _client.IndexAsync(indexDoc, i => i.Index(_options.IndexName).Id(document.Id.ToString()), ct);
        if (!response.IsSuccess())
            _logger.LogError("Elasticsearch index failed for {DocumentId}", document.Id);
    }

    public async Task UpdateDocumentIndexAsync(Document document, CancellationToken ct = default)
    {
        var indexDoc = MapToIndexDocument(document);
        var response = await _client.UpdateAsync<DocumentIndexModel, DocumentIndexModel>(
            _options.IndexName, document.Id.ToString(), u => u.Doc(indexDoc), ct);
        if (!response.IsSuccess())
            await IndexDocumentAsync(document, ct);
    }

    public async Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default)
        => await _client.DeleteAsync<DocumentIndexModel>(documentId.ToString(), d => d.Index(_options.IndexName), ct);

    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        var from = (query.Page - 1) * query.PageSize;
        var response = await _client.SearchAsync<DocumentIndexModel>(s => s
            .Index(_options.IndexName)
            .From(from)
            .Size(query.PageSize), ct);

        if (!response.IsSuccess())
            return new SearchResult(Array.Empty<SearchHit>(), 0, query.Page, query.PageSize);

        var hits = response.Hits
            .Where(hit => hit.Source is not null)
            .Where(hit => string.IsNullOrWhiteSpace(query.Keyword)
                       || hit.Source!.Title.Contains(query.Keyword!, StringComparison.OrdinalIgnoreCase)
                       || hit.Source.DocumentCode.Contains(query.Keyword!, StringComparison.OrdinalIgnoreCase)
                       || (hit.Source.Description?.Contains(query.Keyword!, StringComparison.OrdinalIgnoreCase) ?? false)
                       || hit.Source.Tags.Any(t => t.Contains(query.Keyword!, StringComparison.OrdinalIgnoreCase)))
            .Where(hit => !query.Standard.HasValue || hit.Source!.IsoStandard == query.Standard.Value.ToString())
            .Where(hit => !query.Status.HasValue || hit.Source!.Status == query.Status.Value.ToString())
            .Where(hit => !query.Category.HasValue || hit.Source!.Category == query.Category.Value.ToString())
            .Where(hit => !query.OwnerId.HasValue || hit.Source!.OwnerId == query.OwnerId.Value.ToString())
            .Where(hit => !query.FromDate.HasValue || hit.Source!.UpdatedAt >= query.FromDate.Value)
            .Where(hit => !query.ToDate.HasValue || hit.Source!.UpdatedAt <= query.ToDate.Value)
            .Select(hit =>
            new SearchHit(
                hit.Source!.Id,
                hit.Source.DocumentCode,
                hit.Source.Title,
                hit.Source.Status,
                hit.Source.IsoStandard,
                hit.Score ?? 0,
                Array.Empty<string>()))
            .ToList();

        return new SearchResult(hits, (int)response.Total, query.Page, query.PageSize);
    }

    private static DocumentIndexModel MapToIndexDocument(Document document) => new()
    {
        Id = document.Id,
        DocumentCode = document.Code.Value,
        Title = document.Title,
        Description = document.Description,
        IsoStandard = document.Standard.ToString(),
        Category = document.Category.ToString(),
        Status = document.Status.ToString(),
        Tags = document.Tags.ToArray(),
        OwnerId = document.OwnerId.ToString(),
        UpdatedAt = document.UpdatedAt
    };
}

public sealed class DocumentIndexModel
{
    public Guid Id { get; set; }
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string IsoStandard { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string OwnerId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class ElasticsearchOptions
{
    public const string Section = "Elasticsearch";
    public string Uri { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "iso-documents";
    public string? ApiKey { get; set; }
}
