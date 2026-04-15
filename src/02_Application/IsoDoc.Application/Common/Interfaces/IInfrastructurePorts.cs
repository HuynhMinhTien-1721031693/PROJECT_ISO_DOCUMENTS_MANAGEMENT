using System.Security.Claims;

namespace IsoDoc.Application.Common.Interfaces;

/// <summary>
/// Issues and validates JWT access tokens (implemented in Infrastructure).
/// </summary>
public interface IJwtTokenService
{
    string CreateAccessToken(Guid userId, string? email, IEnumerable<string> roles, TimeSpan? lifetime = null);

    ClaimsPrincipal? ValidateAccessToken(string token);
}

/// <summary>
/// Distributed cache abstraction (Redis in production).
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? absoluteExpiration, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string keyPrefix, CancellationToken ct = default);
}
