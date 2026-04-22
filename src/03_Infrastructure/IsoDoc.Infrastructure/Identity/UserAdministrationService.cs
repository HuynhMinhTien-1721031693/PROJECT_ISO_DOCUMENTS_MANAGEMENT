using IsoDoc.Application.Common.Identity;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Application.Common.Models;
using IsoDoc.Infrastructure.Persistence;
using IsoDoc.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IsoDoc.Infrastructure.Identity;

public sealed class UserAdministrationService : IUserAdministrationService
{
    private static readonly HashSet<string> AllowedRoles =
        new(IsoDocRoles.All, StringComparer.OrdinalIgnoreCase);

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;

    public UserAdministrationService(UserManager<ApplicationUser> userManager, AppDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    public async Task<Result<PagedList<UserListItemDto>>> GetUsersAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<UserListItemDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            items.Add(await MapAsync(u, roles, ct));
        }

        return Result<PagedList<UserListItemDto>>.Success(new PagedList<UserListItemDto>(items, total, page, pageSize));
    }

    public async Task<Result<UserListItemDto>> CreateUserAsync(
        string email,
        string password,
        string? displayName,
        Guid? departmentId,
        IReadOnlyList<string> roles,
        CancellationToken ct)
    {
        var roleResult = ValidateRoles(roles);
        if (!roleResult.IsSuccess)
            return Result<UserListItemDto>.Failure(roleResult.Error!, roleResult.ErrorCode!);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim(),
            UserName = email.Trim(),
            DisplayName = displayName,
            DepartmentId = departmentId
        };

        var create = await _userManager.CreateAsync(user, password);
        if (!create.Succeeded)
            return Result<UserListItemDto>.Failure(string.Join("; ", create.Errors.Select(e => e.Description)), "IDENTITY_ERROR");

        if (roles.Count > 0)
        {
            var addRoles = await _userManager.AddToRolesAsync(user, roles.Distinct(StringComparer.OrdinalIgnoreCase));
            if (!addRoles.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                return Result<UserListItemDto>.Failure(string.Join("; ", addRoles.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
            }
        }

        var created = await _userManager.FindByIdAsync(user.Id.ToString());
        if (created is null)
            return Result<UserListItemDto>.Failure("Không tải lại được người dùng vừa tạo.", "IDENTITY_ERROR");

        var createdRoles = await _userManager.GetRolesAsync(created);
        return Result<UserListItemDto>.Success(await MapAsync(created, createdRoles, ct));
    }

    public async Task<Result> UpdateUserAsync(
        Guid id,
        string? displayName,
        Guid? departmentId,
        bool? lockoutEnabled,
        CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.Failure("Không tìm thấy người dùng.", "USER_NOT_FOUND");

        user.DisplayName = displayName;
        user.DepartmentId = departmentId;

        if (lockoutEnabled.HasValue)
            await _userManager.SetLockoutEnabledAsync(user, lockoutEnabled.Value);

        var update = await _userManager.UpdateAsync(user);
        return update.Succeeded
            ? Result.Success()
            : Result.Failure(string.Join("; ", update.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
    }

    public async Task<Result> SetRolesAsync(Guid id, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var roleResult = ValidateRoles(roles);
        if (!roleResult.IsSuccess)
            return roleResult;

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.Failure("Không tìm thấy người dùng.", "USER_NOT_FOUND");

        var current = await _userManager.GetRolesAsync(user);
        var toRemove = current.Except(roles, StringComparer.OrdinalIgnoreCase).ToArray();
        var toAdd = roles.Except(current, StringComparer.OrdinalIgnoreCase).ToArray();

        if (toRemove.Length > 0)
        {
            var r = await _userManager.RemoveFromRolesAsync(user, toRemove);
            if (!r.Succeeded)
                return Result.Failure(string.Join("; ", r.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
        }

        if (toAdd.Length > 0)
        {
            var a = await _userManager.AddToRolesAsync(user, toAdd);
            if (!a.Succeeded)
                return Result.Failure(string.Join("; ", a.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
        }

        return Result.Success();
    }

    public async Task<Result> LockUserAsync(Guid id, Guid currentUserId, CancellationToken ct)
    {
        if (id == currentUserId)
            return Result.Failure("Không thể khóa chính tài khoản đang đăng nhập.", "SELF_ACTION_FORBIDDEN");

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.Failure("Không tìm thấy người dùng.", "USER_NOT_FOUND");

        await _userManager.SetLockoutEnabledAsync(user, true);
        var until = DateTimeOffset.UtcNow.AddYears(100);
        var setEnd = await _userManager.SetLockoutEndDateAsync(user, until);
        return setEnd.Succeeded
            ? Result.Success()
            : Result.Failure(string.Join("; ", setEnd.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
    }

    public async Task<Result> UnlockUserAsync(Guid id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.Failure("Không tìm thấy người dùng.", "USER_NOT_FOUND");

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        return Result.Success();
    }

    public async Task<Result> DeleteUserAsync(Guid id, Guid currentUserId, CancellationToken ct)
    {
        if (id == currentUserId)
            return Result.Failure("Không thể xóa chính tài khoản đang đăng nhập.", "SELF_ACTION_FORBIDDEN");

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return Result.Failure("Không tìm thấy người dùng.", "USER_NOT_FOUND");

        if (await _userManager.IsInRoleAsync(user, IsoDocRoles.SystemAdmin))
        {
            var admins = await _userManager.GetUsersInRoleAsync(IsoDocRoles.SystemAdmin);
            if (admins.Count <= 1)
                return Result.Failure("Không thể xóa quản trị viên hệ thống cuối cùng.", "LAST_SYSTEM_ADMIN");
        }

        var del = await _userManager.DeleteAsync(user);
        return del.Succeeded
            ? Result.Success()
            : Result.Failure(string.Join("; ", del.Errors.Select(e => e.Description)), "IDENTITY_ERROR");
    }

    private static Result ValidateRoles(IReadOnlyList<string> roles)
    {
        foreach (var r in roles)
        {
            if (!AllowedRoles.Contains(r))
                return Result.Failure($"Vai trò không hợp lệ: {r}", "INVALID_ROLE");
        }

        return Result.Success();
    }

    private async Task<UserListItemDto> MapAsync(ApplicationUser u, IList<string> roles, CancellationToken _)
    {
        var lockedOut = await _userManager.IsLockedOutAsync(u);
        return new UserListItemDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            DisplayName = u.DisplayName,
            DepartmentId = u.DepartmentId,
            LockoutEnabled = await _userManager.GetLockoutEnabledAsync(u),
            LockedOut = lockedOut,
            Roles = roles.ToList()
        };
    }
}
