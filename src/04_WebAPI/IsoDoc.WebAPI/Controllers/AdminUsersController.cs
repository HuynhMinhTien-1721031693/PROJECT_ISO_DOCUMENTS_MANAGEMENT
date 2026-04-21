using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Controllers;

[Route("api/v1/admin/users")]
[Authorize(Policy = "RequireSystemAdmin")]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly IUserAdministrationService _users;

    public AdminUsersController(IUserAdministrationService users) => _users = users;

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _users.GetUsersAsync(page, pageSize, search, ct);
        return FromAdminResult(result, r => PagedResult(r));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateAdminUserRequest body, CancellationToken ct = default)
    {
        var result = await _users.CreateUserAsync(
            body.Email,
            body.Password,
            body.DisplayName,
            body.DepartmentId,
            body.Roles ?? Array.Empty<string>(),
            ct);
        return FromAdminResult(result, r => OkResult(r));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAdminUserRequest body, CancellationToken ct = default)
    {
        var result = await _users.UpdateUserAsync(id, body.DisplayName, body.DepartmentId, body.LockoutEnabled, ct);
        return FromAdminResult(result, () => NoContent());
    }

    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetAdminUserRolesRequest body, CancellationToken ct = default)
    {
        var result = await _users.SetRolesAsync(id, body.Roles ?? Array.Empty<string>(), ct);
        return FromAdminResult(result, () => NoContent());
    }

    private IActionResult FromAdminResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value!);

        if (result.ErrorCode == "DATABASE_REQUIRED")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "User management unavailable",
                Detail = result.Error,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        return UnprocessableEntity(new ProblemDetails { Detail = result.Error, Status = StatusCodes.Status422UnprocessableEntity });
    }

    private IActionResult FromAdminResult(Result result, Func<IActionResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess();

        if (result.ErrorCode == "DATABASE_REQUIRED")
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "User management unavailable",
                Detail = result.Error,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        return UnprocessableEntity(new ProblemDetails { Detail = result.Error, Status = StatusCodes.Status422UnprocessableEntity });
    }
}

public sealed class CreateAdminUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public Guid? DepartmentId { get; set; }
    public IReadOnlyList<string>? Roles { get; set; }
}

public sealed class UpdateAdminUserRequest
{
    public string? DisplayName { get; set; }
    public Guid? DepartmentId { get; set; }
    public bool? LockoutEnabled { get; set; }
}

public sealed class SetAdminUserRolesRequest
{
    public IReadOnlyList<string>? Roles { get; set; }
}
