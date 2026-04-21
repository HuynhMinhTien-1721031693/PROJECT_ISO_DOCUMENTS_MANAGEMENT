using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Documents.Commands.RecordDecision;
using IsoDoc.Application.Documents.Queries.GetPendingApprovals;
using IsoDoc.Application.Documents.Queries.GetWorkflowById;
using IsoDoc.Application.Documents.Queries.SearchDocuments;
using IsoDoc.Domain.Enums;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    [HttpGet("{workflowId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid workflowId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetWorkflowByIdQuery(workflowId), ct);
        return result.IsSuccess ? OkResult(result.Value!) : FromResult(result);
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
    private readonly IUserAuthenticationService _userAuth;

    public AuthController(IJwtTokenService jwt, ICacheService cache, IUserAuthenticationService userAuth)
    {
        _jwt = jwt;
        _cache = cache;
        _userAuth = userAuth;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct = default)
    {
        if (request is null || (string.IsNullOrWhiteSpace(request.Email) && string.IsNullOrWhiteSpace(request.Password)))
        {
            var parsed = await TryReadLoginRequestFromBodyAsync(ct);
            if (parsed is not null)
                request = parsed;
        }
        request ??= new LoginRequest();

        if (request is null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Problem(
                detail: "Email va mat khau la bat buoc.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var (success, user, lockedUntilUtc) =
            await _userAuth.ValidateCredentialsAsync(request.Email, request.Password, ct);

        if (!success)
        {
            if (lockedUntilUtc.HasValue && lockedUntilUtc.Value > DateTime.UtcNow)
            {
                return StatusCode(StatusCodes.Status423Locked, new
                {
                    message = "Tai khoan tam thoi bi khoa do dang nhap sai nhieu lan.",
                    lockedUntilUtc = lockedUntilUtc.Value
                });
            }

            return Unauthorized();
        }

        if (user is null)
            return Unauthorized();

        var accessToken = _jwt.CreateAccessToken(
            user.UserId,
            user.Email,
            user.Roles,
            TimeSpan.FromHours(1),
            user.DisplayName,
            user.DepartmentId);
        var refreshToken = Guid.NewGuid().ToString("N");
        var refreshRecord = new RefreshTokenRecord
        {
            UserId = user.UserId,
            Email = user.Email,
            DisplayName = user.DisplayName,
            DepartmentId = user.DepartmentId,
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

    private async Task<LoginRequest?> TryReadLoginRequestFromBodyAsync(CancellationToken ct)
    {
        try
        {
            Request.EnableBuffering();
            Request.Body.Position = 0;
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            var raw = await reader.ReadToEndAsync(ct);
            Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var parsed = JsonSerializer.Deserialize<LoginRequest>(raw);
            if (parsed is not null)
                return parsed;

            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind is JsonValueKind.String)
            {
                var inner = doc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(inner))
                    return JsonSerializer.Deserialize<LoginRequest>(inner);
            }
        }
        catch
        {
            // Keep authentication flow resilient; validation below handles null/empty values.
        }

        return null;
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct = default)
    {
        var existing = await _cache.GetAsync<RefreshTokenRecord>(RefreshCacheKey(request.RefreshToken), ct);
        if (existing is null || existing.ExpiresAtUtc <= DateTime.UtcNow)
            return Unauthorized();

        var newAccessToken = _jwt.CreateAccessToken(
            existing.UserId,
            existing.Email,
            existing.Roles,
            TimeSpan.FromHours(1),
            existing.DisplayName,
            existing.DepartmentId);
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

    private static string RefreshCacheKey(string token) => $"auth:refresh:{token}";
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

public sealed record RefreshTokenRecord
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? DepartmentId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public DateTime ExpiresAtUtc { get; init; }
}
