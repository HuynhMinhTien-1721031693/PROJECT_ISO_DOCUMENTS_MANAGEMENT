using IsoDoc.Domain.Events;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.EventHandlers;

public sealed class DocumentIndexSyncEventHandler : INotificationHandler<DocumentIndexSyncEvent>
{
    private readonly ISearchService _search;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<DocumentIndexSyncEventHandler> _logger;

    public DocumentIndexSyncEventHandler(
        ISearchService search,
        IDocumentRepository documents,
        ILogger<DocumentIndexSyncEventHandler> logger)
    {
        _search = search;
        _documents = documents;
        _logger = logger;
    }

    public async Task Handle(DocumentIndexSyncEvent notification, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {Id} not found for search index sync.", notification.DocumentId);
            return;
        }

        await _search.UpdateDocumentIndexAsync(document, ct);
        _logger.LogDebug("Search index synced for document {DocumentId}", notification.DocumentId);
    }
}
