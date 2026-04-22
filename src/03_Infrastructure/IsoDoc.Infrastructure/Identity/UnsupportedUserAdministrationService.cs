using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;

namespace IsoDoc.Infrastructure.Identity;

public sealed class UnsupportedUserAdministrationService : IUserAdministrationService
{
    public Task<Result<PagedList<UserListItemDto>>> GetUsersAsync(int page, int pageSize, string? search, CancellationToken ct) =>
        Task.FromResult(Result<PagedList<UserListItemDto>>.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result<UserListItemDto>> CreateUserAsync(
        string email,
        string password,
        string? displayName,
        Guid? departmentId,
        IReadOnlyList<string> roles,
        CancellationToken ct) =>
        Task.FromResult(Result<UserListItemDto>.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result> UpdateUserAsync(Guid id, string? displayName, Guid? departmentId, bool? lockoutEnabled, CancellationToken ct) =>
        Task.FromResult(Result.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result> SetRolesAsync(Guid id, IReadOnlyList<string> roles, CancellationToken ct) =>
        Task.FromResult(Result.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result> LockUserAsync(Guid id, Guid currentUserId, CancellationToken ct) =>
        Task.FromResult(Result.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result> UnlockUserAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Result.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));

    public Task<Result> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct) =>
        Task.FromResult(Result.Failure(
            "Quản lý người dùng cần SQL Server (DefaultConnection hoặc SqlServer).",
            "DATABASE_REQUIRED"));
}
