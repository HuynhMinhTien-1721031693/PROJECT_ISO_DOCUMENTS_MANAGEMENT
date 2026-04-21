using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Events;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.EventHandlers;

public sealed class DocumentCreatedEventHandler : INotificationHandler<DocumentCreatedEvent>
{
    private readonly ISearchService _search;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<DocumentCreatedEventHandler> _logger;

    public DocumentCreatedEventHandler(
        ISearchService search,
        IDocumentRepository documents,
        ILogger<DocumentCreatedEventHandler> logger)
    {
        _search = search;
        _documents = documents;
        _logger = logger;
    }

    public async Task Handle(DocumentCreatedEvent notification, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {Id} not found for initial index.", notification.DocumentId);
            return;
        }

        await _search.IndexDocumentAsync(document, ct);
        _logger.LogInformation("Indexed new document {DocumentId}", notification.DocumentId);
    }
}

public sealed class DocumentPublishedEventHandler : INotificationHandler<DocumentPublishedEvent>
{
    private readonly IDocumentRepository _documents;
    private readonly INotificationSender _notifications;
    private readonly ILogger<DocumentPublishedEventHandler> _logger;

    public DocumentPublishedEventHandler(
        IDocumentRepository documents,
        INotificationSender notifications,
        ILogger<DocumentPublishedEventHandler> logger)
    {
        _documents = documents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(DocumentPublishedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling DocumentPublishedEvent for {Code} v{Version}",
            notification.DocumentCode, notification.Version);

        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {Id} not found for publish notification.", notification.DocumentId);
            return;
        }

        try
        {
            await _notifications.SendInAppNotificationAsync(
                userId: document.OwnerId,
                title: "Tài liệu đã được phát hành",
                message: $"Tài liệu {notification.DocumentCode} v{notification.Version} đã được phê duyệt và phát hành.",
                actionUrl: $"/documents/{notification.DocumentId}",
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send publish notification for {Code}", notification.DocumentCode);
        }
    }
}

public sealed class DocumentRejectedEventHandler : INotificationHandler<DocumentRejectedEvent>
{
    private readonly IDocumentRepository _documents;
    private readonly INotificationSender _notifications;
    private readonly ILogger<DocumentRejectedEventHandler> _logger;

    public DocumentRejectedEventHandler(
        IDocumentRepository documents,
        INotificationSender notifications,
        ILogger<DocumentRejectedEventHandler> logger)
    {
        _documents = documents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(DocumentRejectedEvent notification, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
            return;

        await _notifications.SendInAppNotificationAsync(
            userId: document.OwnerId,
            title: "Tài liệu bị từ chối",
            message: $"Tài liệu {document.Code.Value} đã bị từ chối. Lý do: {notification.Reason}",
            actionUrl: $"/documents/{document.Id}",
            ct: ct);

        _logger.LogInformation(
            "Rejection notification sent to owner {OwnerId} for document {Code}",
            document.OwnerId, document.Code);
    }
}

public sealed class WorkflowStepAdvancedEventHandler : INotificationHandler<WorkflowStepAdvancedEvent>
{
    private readonly INotificationSender _notifications;
    private readonly IDocumentRepository _documents;
    private readonly ILogger<WorkflowStepAdvancedEventHandler> _logger;

    public WorkflowStepAdvancedEventHandler(
        INotificationSender notifications,
        IDocumentRepository documents,
        ILogger<WorkflowStepAdvancedEventHandler> logger)
    {
        _notifications = notifications;
        _documents = documents;
        _logger = logger;
    }

    public async Task Handle(WorkflowStepAdvancedEvent notification, CancellationToken ct)
    {
        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
            return;

        await _notifications.SendInAppNotificationAsync(
            userId: notification.NextApproverId,
            title: $"Cần phê duyệt: {document.Code.Value}",
            message: $"Tài liệu '{document.Title}' đang chờ phê duyệt bước {notification.NextStepOrder} của bạn.",
            actionUrl: $"/workflow/{notification.WorkflowId}",
            ct: ct);

        _logger.LogInformation(
            "Approval request sent to {ApproverId} for document {Code} step {Step}",
            notification.NextApproverId, document.Code, notification.NextStepOrder);
    }
}
