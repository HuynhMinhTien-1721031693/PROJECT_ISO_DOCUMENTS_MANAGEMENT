using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace IsoDoc.Infrastructure.Identity;

public sealed class IdentityUserAuthenticationService : IUserAuthenticationService
{
    private readonly UserManager<ApplicationUser> _userManager;

    public IdentityUserAuthenticationService(UserManager<ApplicationUser> userManager) =>
        _userManager = userManager;

    public async Task<(bool Success, AuthenticatedUserContext? User, DateTime? LockedUntilUtc)> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
            return (false, null, null);

        if (await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            return (false, null, lockoutEnd?.UtcDateTime);
        }

        var validPassword = await _userManager.CheckPasswordAsync(user, password);
        if (!validPassword)
        {
            await _userManager.AccessFailedAsync(user);
            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                return (false, null, lockoutEnd?.UtcDateTime);
            }

            return (false, null, null);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        var roles = await _userManager.GetRolesAsync(user);
        var ctx = new AuthenticatedUserContext
        {
            UserId = user.Id,
            Email = user.Email ?? email.Trim(),
            DisplayName = user.DisplayName,
            DepartmentId = user.DepartmentId,
            Roles = roles.ToList()
        };
        return (true, ctx, null);
    }
}
