using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Infrastructure.Search;

/// <summary>
/// Delegates indexing to Elasticsearch; search falls back to <see cref="IDocumentRepository.SearchDocumentsAsync"/>
/// when Elasticsearch throws or returns an error response.
/// </summary>
public sealed class ResilientSearchService : ISearchService
{
    private readonly ElasticsearchService _elasticsearch;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<ResilientSearchService> _logger;

    public ResilientSearchService(
        ElasticsearchService elasticsearch,
        IDocumentRepository documents,
        ILogger<ResilientSearchService> logger)
    {
        _elasticsearch = elasticsearch;
        _documents = documents;
        _logger = logger;
    }

    public Task IndexDocumentAsync(Document document, CancellationToken ct = default)
        => _elasticsearch.IndexDocumentAsync(document, ct);

    public Task UpdateDocumentIndexAsync(Document document, CancellationToken ct = default)
        => _elasticsearch.UpdateDocumentIndexAsync(document, ct);

    public Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default)
        => _elasticsearch.RemoveDocumentAsync(documentId, ct);

    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        try
        {
            return await _elasticsearch.SearchAsync(query, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Elasticsearch search failed; using SQL fallback.");
            return await _documents.SearchDocumentsAsync(query, ct);
        }
    }
}
