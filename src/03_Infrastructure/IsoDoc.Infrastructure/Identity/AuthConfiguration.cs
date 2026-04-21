namespace IsoDoc.Infrastructure.Identity;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>
    /// When true, <see cref="Users"/> from configuration are created/updated at startup (development or explicit bootstrap only).
    /// </summary>
    public bool SeedUsersFromConfig { get; set; }

    public bool AllowPlainTextForDev { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 5;
    public int FailedAttemptWindowMinutes { get; set; } = 15;
    public int LockoutMinutes { get; set; } = 15;

    public List<AuthUserEntry> Users { get; set; } = new();
}

public sealed class AuthUserEntry
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>When set, included in JWT so uploads and ownership checks work in dev/config-file auth mode.</summary>
    public Guid? DepartmentId { get; set; }
    public List<string> Roles { get; set; } = new();
}
