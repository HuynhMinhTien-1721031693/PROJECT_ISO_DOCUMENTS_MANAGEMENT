using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Search;

public sealed class ElasticsearchService : ISearchService
{
    private static readonly SemaphoreSlim EnsureIndexLock = new(1, 1);
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchService> _logger;
    private bool _indexEnsured;

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
        try
        {
            await EnsureIndexExistsAsync(ct);
            var indexDoc = MapToIndexDocument(document);
            var response = await _client.IndexAsync(indexDoc, i => i.Index(_options.IndexName).Id(document.Id.ToString()), ct);
            if (!response.IsSuccess())
            {
                _logger.LogError(
                    "Elasticsearch index failed for {DocumentId}: {Debug}",
                    document.Id,
                    response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch index exception for {DocumentId}", document.Id);
        }
    }

    public async Task UpdateDocumentIndexAsync(Document document, CancellationToken ct = default)
    {
        try
        {
            await EnsureIndexExistsAsync(ct);
            var indexDoc = MapToIndexDocument(document);
            var response = await _client.UpdateAsync<DocumentIndexModel, DocumentIndexModel>(
                _options.IndexName, document.Id.ToString(), u => u.Doc(indexDoc), ct);
            if (!response.IsSuccess())
            {
                _logger.LogWarning(
                    "Elasticsearch update failed for {DocumentId}, retrying as index. {Debug}",
                    document.Id,
                    response.DebugInformation);
                await IndexDocumentAsync(document, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch update exception for {DocumentId}", document.Id);
        }
    }

    public async Task RemoveDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        try
        {
            await EnsureIndexExistsAsync(ct);
            var response = await _client.DeleteAsync<DocumentIndexModel>(documentId.ToString(), d => d.Index(_options.IndexName), ct);
            if (!response.IsSuccess())
            {
                _logger.LogWarning(
                    "Elasticsearch delete failed for {DocumentId}: {Debug}",
                    documentId,
                    response.DebugInformation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch delete exception for {DocumentId}", documentId);
        }
    }

    public async Task<SearchResult> SearchAsync(SearchQuery query, CancellationToken ct = default)
    {
        try
        {
            await EnsureIndexExistsAsync(ct);
            var from = (query.Page - 1) * query.PageSize;
            SearchResponse<DocumentIndexModel> response;
            if (string.IsNullOrWhiteSpace(query.Keyword))
            {
                response = await _client.SearchAsync<DocumentIndexModel>(s => s
                    .Index(_options.IndexName)
                    .From(from)
                    .Size(query.PageSize)
                    .Query(q => q.MatchAll(new MatchAllQuery())), ct);
            }
            else
            {
                response = await _client.SearchAsync<DocumentIndexModel>(s => s
                    .Index(_options.IndexName)
                    .From(from)
                    .Size(query.PageSize)
                    .Query(q => q.SimpleQueryString(sqs => sqs
                        .Query(query.Keyword!)
                        .Fields(new[] { "documentCode^4", "title^3", "description^2", "tags^2" }))), ct);
            }

            if (!response.IsSuccess())
            {
                _logger.LogError(
                    "Elasticsearch search failed: {Debug}",
                    response.DebugInformation);
                throw new ElasticsearchSearchException(
                    $"Elasticsearch search failed: {response.ElasticsearchServerError?.Error?.Reason ?? response.DebugInformation}");
            }

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
                {
                    return new SearchHit(
                        hit.Source!.Id,
                        hit.Source.DocumentCode,
                        hit.Source.Title,
                        hit.Source.Status,
                        hit.Source.Category,
                        hit.Source.IsoStandard,
                        hit.Source.UpdatedAt,
                        hit.Source.CurrentVersion ?? string.Empty,
                        hit.Score ?? 0,
                        Array.Empty<string>());
                })
                .ToList();

            return new SearchResult(hits, (int)response.Total, query.Page, query.PageSize);
        }
        catch (ElasticsearchSearchException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Elasticsearch search threw an exception.");
            throw new ElasticsearchSearchException("Elasticsearch search failed.", ex);
        }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken ct)
    {
        if (_indexEnsured)
            return;

        await EnsureIndexLock.WaitAsync(ct);
        try
        {
            if (_indexEnsured)
                return;

            var exists = await _client.Indices.ExistsAsync(_options.IndexName, ct);
            if (!exists.Exists)
            {
                var create = await _client.Indices.CreateAsync(_options.IndexName, _ => { }, ct);
                if (!create.IsSuccess())
                {
                    _logger.LogWarning(
                        "Could not create Elasticsearch index {IndexName}: {Debug}",
                        _options.IndexName,
                        create.DebugInformation);
                }
            }

            _indexEnsured = true;
        }
        finally
        {
            EnsureIndexLock.Release();
        }
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
        CurrentVersion = document.CurrentVersion.ToString(),
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
    public string CurrentVersion { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public string OwnerId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public sealed class ElasticsearchOptions
{
    public const string Section = "IsoDoc:Elasticsearch";
    public string Uri { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "iso-documents";
    public string? ApiKey { get; set; }

    /// <summary>When true, registers <see cref="ElasticsearchHealthCheck"/> (degraded if ES is down).</summary>
    public bool ParticipateInHealthChecks { get; set; }
}
