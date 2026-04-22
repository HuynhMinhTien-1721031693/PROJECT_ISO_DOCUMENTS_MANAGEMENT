using IsoDoc.Application.Common.Models;

namespace IsoDoc.Application.Common.Interfaces;

public sealed class AuthenticatedUserContext
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? DepartmentId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public sealed class UserListItemDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? DepartmentId { get; init; }
    public bool LockoutEnabled { get; init; }
    public bool LockedOut { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}

public interface IUserAuthenticationService
{
    Task<(bool Success, AuthenticatedUserContext? User, DateTime? LockedUntilUtc)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default);
}

public interface IUserAdministrationService
{
    Task<Result<PagedList<UserListItemDto>>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct);

    Task<Result<UserListItemDto>> CreateUserAsync(
        string email,
        string password,
        string? displayName,
        Guid? departmentId,
        IReadOnlyList<string> roles,
        CancellationToken ct);

    Task<Result> UpdateUserAsync(Guid id, string? displayName, Guid? departmentId, bool? lockoutEnabled, CancellationToken ct);

    Task<Result> SetRolesAsync(Guid id, IReadOnlyList<string> roles, CancellationToken ct);

    Task<Result> LockUserAsync(Guid id, Guid currentUserId, CancellationToken ct);

    Task<Result> UnlockUserAsync(Guid id, CancellationToken ct);

    Task<Result> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct);
}
