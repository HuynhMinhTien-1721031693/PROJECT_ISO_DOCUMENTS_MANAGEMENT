using System.Security.Cryptography;
using IsoDoc.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Identity;

/// <summary>
/// Fallback authentication when no SQL database is configured (local development only).
/// </summary>
public sealed class ConfigFileUserAuthenticationService : IUserAuthenticationService
{
    private readonly ICacheService _cache;
    private readonly AuthOptions _options;
    private readonly IHostEnvironment _environment;

    public ConfigFileUserAuthenticationService(
        ICacheService cache,
        IOptions<AuthOptions> options,
        IHostEnvironment environment)
    {
        _cache = cache;
        _options = options.Value;
        _environment = environment;
    }

    public async Task<(bool Success, AuthenticatedUserContext? User, DateTime? LockedUntilUtc)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment()
            && !string.Equals(_environment.EnvironmentName, "IntegrationTests", StringComparison.OrdinalIgnoreCase))
            return (false, null, null);

        var emailKey = email.Trim().ToLowerInvariant();
        var lockoutKey = LockoutCacheKey(emailKey);
        var failKey = FailedLoginCacheKey(emailKey);

        var lockout = await _cache.GetAsync<LoginLockoutRecord>(lockoutKey, ct);
        if (lockout is not null && lockout.LockedUntilUtc > DateTime.UtcNow)
            return (false, null, lockout.LockedUntilUtc);

        var user = ResolveUser(email);
        if (user is null)
            return (false, null, null);

        var passwordOk = VerifyPassword(password, user.Password, _options);
        if (!passwordOk)
        {
            var failed = await _cache.GetAsync<FailedLoginRecord>(failKey, ct) ?? new FailedLoginRecord();
            failed = failed with { Count = failed.Count + 1, LastFailedAtUtc = DateTime.UtcNow };
            await _cache.SetAsync(failKey, failed, TimeSpan.FromMinutes(_options.FailedAttemptWindowMinutes), ct);

            if (failed.Count >= _options.MaxFailedAttempts)
            {
                var lockRecord = new LoginLockoutRecord
                {
                    LockedUntilUtc = DateTime.UtcNow.AddMinutes(_options.LockoutMinutes)
                };
                await _cache.SetAsync(lockoutKey, lockRecord, TimeSpan.FromMinutes(_options.LockoutMinutes), ct);
            }

            return (false, null, null);
        }

        await _cache.RemoveAsync(failKey, ct);
        await _cache.RemoveAsync(lockoutKey, ct);

        var ctx = new AuthenticatedUserContext
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = null,
            DepartmentId = user.DepartmentId,
            Roles = user.Roles
        };
        return (true, ctx, null);
    }

    private AuthUserEntry? ResolveUser(string email)
    {
        var users = _options.Users;
        if (users.Count == 0)
            return null;

        return users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    private static string FailedLoginCacheKey(string email) => $"auth:failed:{email}";
    private static string LockoutCacheKey(string email) => $"auth:lockout:{email}";

    private static bool VerifyPassword(string providedPassword, string storedPassword, AuthOptions options)
    {
        if (string.IsNullOrWhiteSpace(storedPassword))
            return false;

        if (storedPassword.StartsWith("pbkdf2$", StringComparison.OrdinalIgnoreCase))
            return VerifyPbkdf2Password(providedPassword, storedPassword);

        return options.AllowPlainTextForDev && string.Equals(providedPassword, storedPassword, StringComparison.Ordinal);
    }

    private static bool VerifyPbkdf2Password(string providedPassword, string encoded)
    {
        var parts = encoded.Split('$');
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
            return false;

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedHash = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password: providedPassword,
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private sealed record FailedLoginRecord
    {
        public int Count { get; init; }
        public DateTime LastFailedAtUtc { get; init; }
    }

    private sealed record LoginLockoutRecord
    {
        public DateTime LockedUntilUtc { get; init; }
    }
}
