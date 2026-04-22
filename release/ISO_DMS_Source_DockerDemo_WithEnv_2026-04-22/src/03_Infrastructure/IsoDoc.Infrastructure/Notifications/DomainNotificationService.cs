using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;

namespace IsoDoc.Infrastructure.Notifications;

public sealed class DomainNotificationService : INotificationService
{
    private readonly INotificationSender _sender;

    public DomainNotificationService(INotificationSender sender)
    {
        _sender = sender;
    }

    public async Task SendWorkflowNotificationAsync(
        Guid recipientUserId,
        string subject,
        string message,
        CancellationToken ct = default)
    {
        // In this version we route workflow notifications to in-app channel.
        await _sender.SendInAppNotificationAsync(recipientUserId, subject, message, null, ct);
    }
}
