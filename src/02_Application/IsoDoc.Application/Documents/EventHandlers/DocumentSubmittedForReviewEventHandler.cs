using System.Linq;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Events;
using IsoDoc.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Documents.EventHandlers;

public sealed class DocumentSubmittedForReviewEventHandler : INotificationHandler<DocumentSubmittedForReviewEvent>
{
    private readonly IApprovalWorkflowRepository _workflows;
    private readonly IDocumentRepository _documents;
    private readonly INotificationSender _notifications;
    private readonly ILogger<DocumentSubmittedForReviewEventHandler> _logger;

    public DocumentSubmittedForReviewEventHandler(
        IApprovalWorkflowRepository workflows,
        IDocumentRepository documents,
        INotificationSender notifications,
        ILogger<DocumentSubmittedForReviewEventHandler> logger)
    {
        _workflows = workflows;
        _documents = documents;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task Handle(DocumentSubmittedForReviewEvent notification, CancellationToken ct)
    {
        var workflow = await _workflows.GetActiveWorkflowAsync(notification.DocumentId, ct);
        if (workflow is null)
        {
            _logger.LogWarning("No active workflow for document {DocumentId} after submit.", notification.DocumentId);
            return;
        }

        var step1 = workflow.Steps.OrderBy(s => s.StepOrder).FirstOrDefault();
        if (step1 is null)
            return;

        var document = await _documents.GetByIdAsync(notification.DocumentId, ct);
        if (document is null)
            return;

        await _notifications.SendInAppNotificationAsync(
            userId: step1.ApproverId,
            title: $"Cần phê duyệt: {document.Code.Value}",
            message: $"Tài liệu '{document.Title}' vừa được gửi và đang chờ phê duyệt bước 1.",
            actionUrl: $"/workflow/{workflow.Id}",
            ct: ct);

        _logger.LogInformation(
            "Notified first approver {ApproverId} for document {Code}",
            step1.ApproverId, document.Code);
    }
}
