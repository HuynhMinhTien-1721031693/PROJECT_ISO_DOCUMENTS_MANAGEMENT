using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Documents.Commands.RecordDecision;
using IsoDoc.Application.Documents.Queries.GetPendingApprovals;
using IsoDoc.Application.Documents.Queries.SearchDocuments;
using IsoDoc.Domain.Enums;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/[controller]")]
[Authorize]
public sealed class WorkflowController : ApiControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public WorkflowController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("pending")]
    [Authorize(Policy = "RequireApprover")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var approverId = _currentUser.UserId;
        if (!approverId.HasValue)
            return Unauthorized();

        var result = await Mediator.Send(new GetPendingApprovalsQuery(approverId.Value, page, pageSize), ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }

    [HttpPost("{workflowId:guid}/decision")]
    [Authorize(Policy = "RequireApprover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RecordDecision(
        Guid workflowId,
        [FromBody] DecisionRequest request,
        CancellationToken ct = default)
    {
        var command = new RecordDecisionCommand
        {
            WorkflowId = workflowId,
            Decision = request.Decision,
            Comment = request.Comment
        };
        var result = await Mediator.Send(command, ct);
        return FromResult(result);
    }
}

public sealed class DecisionRequest
{
    public WorkflowDecision Decision { get; set; }
    public string? Comment { get; set; }
}

[Route("api/v1/[controller]")]
[Authorize]
public sealed class SearchController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new SearchDocumentsQuery { Keyword = q, Page = page, PageSize = pageSize }, ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }

    [HttpPost("advanced")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdvancedSearch(
        [FromBody] SearchDocumentsQuery query,
        CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return result.IsSuccess ? PagedResult(result.Value!) : FromResult(result);
    }
}

[Route("api/v1/[controller]")]
public sealed class AuthController : ApiControllerBase
{
    private readonly IJwtTokenService _jwt;
    private readonly ICacheService _cache;
    private readonly IConfiguration _configuration;

    public AuthController(IJwtTokenService jwt, ICacheService cache, IConfiguration configuration)
    {
        _jwt = jwt;
        _cache = cache;
        _configuration = configuration;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Problem(
                detail: "Email va mat khau la bat buoc.",
                statusCode: StatusCodes.Status400BadRequest);

        var options = _configuration.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
        var emailKey = request.Email.Trim().ToLowerInvariant();
        var lockoutKey = LockoutCacheKey(emailKey);
        var failKey = FailedLoginCacheKey(emailKey);

        var lockout = await _cache.GetAsync<LoginLockoutRecord>(lockoutKey, ct);
        if (lockout is not null && lockout.LockedUntilUtc > DateTime.UtcNow)
        {
            return StatusCode(StatusCodes.Status423Locked, new
            {
                message = "Tai khoan tam thoi bi khoa do dang nhap sai nhieu lan.",
                lockedUntilUtc = lockout.LockedUntilUtc
            });
        }

        var user = ResolveUser(request.Email);
        if (user is null)
            return Unauthorized();

        if (!VerifyPassword(request.Password, user.Password, options))
        {
            var failed = await _cache.GetAsync<FailedLoginRecord>(failKey, ct) ?? new FailedLoginRecord();
            failed = failed with { Count = failed.Count + 1, LastFailedAtUtc = DateTime.UtcNow };
            await _cache.SetAsync(failKey, failed, TimeSpan.FromMinutes(options.FailedAttemptWindowMinutes), ct);

            if (failed.Count >= options.MaxFailedAttempts)
            {
                var lockRecord = new LoginLockoutRecord
                {
                    LockedUntilUtc = DateTime.UtcNow.AddMinutes(options.LockoutMinutes)
                };
                await _cache.SetAsync(lockoutKey, lockRecord, TimeSpan.FromMinutes(options.LockoutMinutes), ct);
            }

            return Unauthorized();
        }

        await _cache.RemoveAsync(failKey, ct);
        await _cache.RemoveAsync(lockoutKey, ct);

        var accessToken = _jwt.CreateAccessToken(user.UserId, user.Email, user.Roles, TimeSpan.FromHours(1));
        var refreshToken = Guid.NewGuid().ToString("N");
        var refreshRecord = new RefreshTokenRecord
        {
            UserId = user.UserId,
            Email = user.Email,
            Roles = user.Roles,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };
        await _cache.SetAsync(RefreshCacheKey(refreshToken), refreshRecord, TimeSpan.FromDays(7), ct);

        return OkResult(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct = default)
    {
        var existing = await _cache.GetAsync<RefreshTokenRecord>(RefreshCacheKey(request.RefreshToken), ct);
        if (existing is null || existing.ExpiresAtUtc <= DateTime.UtcNow)
            return Unauthorized();

        var newAccessToken = _jwt.CreateAccessToken(existing.UserId, existing.Email, existing.Roles, TimeSpan.FromHours(1));
        var newRefreshToken = Guid.NewGuid().ToString("N");

        await _cache.RemoveAsync(RefreshCacheKey(request.RefreshToken), ct);
        await _cache.SetAsync(
            RefreshCacheKey(newRefreshToken),
            existing with { ExpiresAtUtc = DateTime.UtcNow.AddDays(7) },
            TimeSpan.FromDays(7),
            ct);

        return OkResult(new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            ExpiresIn = 3600,
            TokenType = "Bearer"
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest? request = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
            await _cache.RemoveAsync(RefreshCacheKey(request.RefreshToken), ct);
        return NoContent();
    }

    private AuthUser? ResolveUser(string email)
    {
        var users = _configuration.GetSection("Auth:Users").Get<List<AuthUser>>() ?? new List<AuthUser>();
        if (users.Count == 0)
        {
            users.Add(new AuthUser
            {
                UserId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "admin@local",
                Password = "Admin@123",
                Roles = new List<string> { "SystemAdmin", "DocumentController" }
            });
        }

        return users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    private static string RefreshCacheKey(string token) => $"auth:refresh:{token}";
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
        // Format: pbkdf2$<iterations>$<saltBase64>$<hashBase64>
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
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AuthUser
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public sealed record RefreshTokenRecord
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public DateTime ExpiresAtUtc { get; init; }
}

public sealed record FailedLoginRecord
{
    public int Count { get; init; }
    public DateTime LastFailedAtUtc { get; init; }
}

public sealed record LoginLockoutRecord
{
    public DateTime LockedUntilUtc { get; init; }
}

public sealed class AuthOptions
{
    public const string Section = "Auth";

    public bool AllowPlainTextForDev { get; set; } = true;
    public int MaxFailedAttempts { get; set; } = 5;
    public int FailedAttemptWindowMinutes { get; set; } = 15;
    public int LockoutMinutes { get; set; } = 15;
}
