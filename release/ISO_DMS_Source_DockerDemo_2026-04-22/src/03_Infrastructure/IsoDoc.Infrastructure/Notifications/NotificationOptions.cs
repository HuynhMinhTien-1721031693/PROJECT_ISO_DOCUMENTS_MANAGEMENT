namespace IsoDoc.Infrastructure.Notifications;

public sealed class NotificationOptions
{
    public const string Section = "Notifications";

    /// <summary>When true and SMTP settings are valid, outbound email is sent via MailKit.</summary>
    public bool EnableSmtp { get; set; }

    /// <summary>When true, SignalR pushes to the recipient after persisting the notification.</summary>
    public bool EnableSignalR { get; set; } = true;

    public string SmtpHost { get; set; } = "localhost";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPass { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@isodms.internal";
    public string FromName { get; set; } = "ISO Document System";

    /// <summary>Use TLS/STARTTLS on connect (typical for port 587).</summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Intranet/dev only: allow invalid TLS certificates when connecting to SMTP.</summary>
    public bool SkipServerCertificateValidation { get; set; }
}
