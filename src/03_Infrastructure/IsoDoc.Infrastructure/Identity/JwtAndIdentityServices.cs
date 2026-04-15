using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IsoDoc.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace IsoDoc.Infrastructure.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options) => _options = options.Value;

    public string CreateAccessToken(Guid userId, string? email, IEnumerable<string> roles, TimeSpan? lifetime = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
            claims.Add(new Claim(ClaimTypes.Email, email));
        }
        claims.AddRange(roles.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(_options.ExpiryMinutes)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateAccessToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContext;

    public CurrentUserService(IHttpContextAccessor httpContext) => _httpContext = httpContext;

    private ClaimsPrincipal? User => _httpContext.HttpContext?.User;

    public Guid? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) is string id
        && Guid.TryParse(id, out var guid) ? guid : null;

    public Guid DepartmentId => Guid.Empty;
    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue(JwtRegisteredClaimNames.Email);
    public string? FullName => User?.FindFirstValue(ClaimTypes.Name) ?? User?.FindFirstValue(JwtRegisteredClaimNames.Name);
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
    public IReadOnlyList<string> Roles => User is null
        ? Array.Empty<string>()
        : User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
}

public sealed class PermissionService : IPermissionService
{
    private readonly IRedisCacheService _cache;
    private readonly ICurrentUserService _currentUser;

    public PermissionService(IRedisCacheService cache, ICurrentUserService currentUser)
    {
        _cache = cache;
        _currentUser = currentUser;
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, ct);
        return permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var cacheKey = $"user-permissions:{userId}";
        var cached = await _cache.GetAsync<List<string>>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var roles = _currentUser.Roles;
        var permissions = ResolvePermissionsFromRoles(roles);
        await _cache.SetAsync(cacheKey, permissions, TimeSpan.FromMinutes(5), ct);
        return permissions;
    }

    private static List<string> ResolvePermissionsFromRoles(IReadOnlyList<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            switch (role)
            {
                case "SystemAdmin":
                    permissions.Add(Permissions.DocumentUpload);
                    permissions.Add(Permissions.DocumentEdit);
                    permissions.Add(Permissions.DocumentDelete);
                    permissions.Add(Permissions.DocumentSubmit);
                    permissions.Add(Permissions.DocumentApprove);
                    permissions.Add(Permissions.DocumentArchive);
                    permissions.Add(Permissions.DocumentViewAll);
                    permissions.Add(Permissions.WorkflowViewPending);
                    permissions.Add(Permissions.UserManage);
                    permissions.Add(Permissions.RoleAssign);
                    permissions.Add(Permissions.AuditLogView);
                    break;

                case "ISOManager":
                    permissions.Add(Permissions.DocumentApprove);
                    permissions.Add(Permissions.DocumentArchive);
                    permissions.Add(Permissions.DocumentViewAll);
                    permissions.Add(Permissions.WorkflowViewPending);
                    break;

                case "DocumentController":
                    permissions.Add(Permissions.DocumentUpload);
                    permissions.Add(Permissions.DocumentEdit);
                    permissions.Add(Permissions.DocumentSubmit);
                    permissions.Add(Permissions.DocumentViewAll);
                    break;

                case "QAOfficer":
                case "SafetyOfficer":
                case "ISMSOfficer":
                    permissions.Add(Permissions.DocumentApprove);
                    permissions.Add(Permissions.WorkflowViewPending);
                    permissions.Add(Permissions.DocumentViewAll);
                    break;
            }
        }

        return permissions.ToList();
    }
}

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string SecretKey { get; set; } = string.Empty;
    // Backward-compatible alias for config key "SigningKey".
    public string SigningKey
    {
        get => SecretKey;
        set => SecretKey = value;
    }
    public string Issuer { get; set; } = "isodms.internal";
    public string Audience { get; set; } = "isodms.client";
    public int ExpiryMinutes { get; set; } = 60;
    // Backward-compatible alias for config key "AccessTokenMinutes".
    public int AccessTokenMinutes
    {
        get => ExpiryMinutes;
        set => ExpiryMinutes = value;
    }
}

public interface IRedisCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan expiry, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}
