using IsoDoc.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IsoDoc.Infrastructure.Identity;

/// <summary>
/// Resolves display names and email addresses from <c>Auth:Users</c> (same source as login).
/// Add all workflow approver accounts to config so UI labels and SMTP routing resolve correctly.
/// </summary>
public sealed class ConfigAuthUserDirectoryService : IUserDirectoryLookup
{
    private readonly IConfiguration _configuration;
    private IReadOnlyDictionary<Guid, (string Email, string DisplayName)>? _byId;

    public ConfigAuthUserDirectoryService(IConfiguration configuration) => _configuration = configuration;

    public string? TryGetEmail(Guid userId)
        => Cache.TryGetValue(userId, out var row) ? row.Email : null;

    public string? TryGetDisplayName(Guid userId)
        => Cache.TryGetValue(userId, out var row) ? row.DisplayName : null;

    private IReadOnlyDictionary<Guid, (string Email, string DisplayName)> Cache =>
        _byId ??= Build();

    private IReadOnlyDictionary<Guid, (string Email, string DisplayName)> Build()
    {
        var users = _configuration.GetSection("Auth:Users").Get<List<AuthUserJson>>() ?? new List<AuthUserJson>();
        var dict = new Dictionary<Guid, (string Email, string DisplayName)>();
        foreach (var u in users)
        {
            if (u.UserId == Guid.Empty || string.IsNullOrWhiteSpace(u.Email))
                continue;
            var label = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Email.Trim() : u.DisplayName.Trim();
            dict[u.UserId] = (u.Email.Trim(), label);
        }

        return dict;
    }

    private sealed class AuthUserJson
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public List<string> Roles { get; set; } = new();
    }
}
