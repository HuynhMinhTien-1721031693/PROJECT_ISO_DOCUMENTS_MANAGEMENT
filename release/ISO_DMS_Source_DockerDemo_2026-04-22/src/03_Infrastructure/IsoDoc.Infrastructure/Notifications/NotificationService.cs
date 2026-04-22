using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IsoDoc.Infrastructure.Notifications;

public sealed class NotificationService : INotificationSender
{
    private readonly NotificationOptions _options;
    private readonly IUserNotificationRepository _notifications;
    private readonly IUserDirectoryLookup _directory;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IOptions<NotificationOptions> options,
        IUserNotificationRepository notifications,
        IUserDirectoryLookup directory,
        IHubContext<NotificationHub> hub,
        ILogger<NotificationService> logger)
    {
        _options = options.Value;
        _notifications = notifications;
        _directory = directory;
        _hub = hub;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (!_options.EnableSmtp || string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogInformation("Email skipped (SMTP disabled or no recipient): {Subject}", subject);
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SmtpHost) || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            _logger.LogWarning("SMTP enabled but SmtpHost/FromAddress not configured; email not sent.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        if (_options.SkipServerCertificateValidation)
        {
            client.ServerCertificateValidationCallback = static (
                object _,
                X509Certificate? __,
                X509Chain? ___,
                SslPolicyErrors ____) => true;
        }

        var secure = _options.SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secure, ct);

        if (!string.IsNullOrEmpty(_options.SmtpUser))
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPass, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("SMTP email sent to {To} subject {Subject}", toEmail, subject);
    }

    public async Task SendInAppNotificationAsync(
        Guid userId,
        string title,
        string message,
        string? actionUrl = null,
        CancellationToken ct = default,
        bool broadcastDocumentApprovedRealtime = false)
    {
        var entity = new UserNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            IsRead = false
        };

        await _notifications.AddAsync(entity, ct);

        if (_options.EnableSignalR)
        {
            try
            {
                var payload = new
                {
                    id = entity.Id,
                    title,
                    message,
                    actionUrl,
                    createdAt = entity.CreatedAt
                };

                await _hub.Clients
                    .User(userId.ToString())
                    .SendAsync(NotificationHub.ReceiveNotification, payload, ct);

                if (broadcastDocumentApprovedRealtime)
                {
                    Guid? documentId = TryParseDocumentIdFromActionUrl(actionUrl);
                    await _hub.Clients
                        .User(userId.ToString())
                        .SendAsync(
                            NotificationHub.DocumentApproved,
                            new
                            {
                                notificationId = entity.Id,
                                documentId,
                                title,
                                message,
                                actionUrl,
                                createdAt = entity.CreatedAt
                            },
                            ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR push failed for user {UserId}", userId);
            }
        }

        var email = _directory.TryGetEmail(userId);
        if (!string.IsNullOrWhiteSpace(email))
        {
            var safe = WebUtility.HtmlEncode(message).Replace("\n", "<br/>", StringComparison.Ordinal);
            var html = $"<p>{safe}</p>";
            if (!string.IsNullOrWhiteSpace(actionUrl))
                html += $"<p><a href=\"{WebUtility.HtmlEncode(actionUrl)}\">Mở trong hệ thống</a></p>";

            try
            {
                await SendEmailAsync(email, title, html, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send workflow email to {Email}", email);
            }
        }
        else
        {
            _logger.LogDebug("No email configured for user {UserId}; in-app notification persisted only.", userId);
        }
    }

    private static Guid? TryParseDocumentIdFromActionUrl(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
            return null;

        var m = Regex.Match(actionUrl, @"documents/([0-9a-fA-F-]{36})", RegexOptions.IgnoreCase);
        return m.Success && Guid.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }
}
