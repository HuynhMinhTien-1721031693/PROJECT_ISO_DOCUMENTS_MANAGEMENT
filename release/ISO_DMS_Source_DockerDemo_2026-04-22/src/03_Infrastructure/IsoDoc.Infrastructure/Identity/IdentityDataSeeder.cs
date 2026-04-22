using IsoDoc.Application.Common.Identity;
using IsoDoc.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Identity;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        if (roleManager is null || userManager is null)
            return;

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var authOptions = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;

        foreach (var roleName in IsoDocRoles.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;
            var r = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName) { Id = Guid.NewGuid() });
            if (!r.Succeeded)
                logger.LogWarning("Could not create role {Role}: {Errors}", roleName, string.Join("; ", r.Errors.Select(e => e.Description)));
        }

        var allowConfigSeed = authOptions.SeedUsersFromConfig && authOptions.Users.Count > 0;
        if (!allowConfigSeed)
            return;

        if (!env.IsDevelopment() && !string.Equals(env.EnvironmentName, "Docker", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "Auth:SeedUsersFromConfig is enabled but skipped because the host is not Development or Docker.");
            return;
        }

        foreach (var entry in authOptions.Users)
        {
            var email = entry.Email.Trim();
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is null)
            {
                var user = new ApplicationUser
                {
                    Id = entry.UserId == Guid.Empty ? Guid.NewGuid() : entry.UserId,
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    DepartmentId = entry.DepartmentId
                };

                var created = await userManager.CreateAsync(user, entry.Password);

                if (!created.Succeeded)
                {
                    logger.LogWarning("Seed user {Email} failed: {Errors}", email, string.Join("; ", created.Errors.Select(e => e.Description)));
                    continue;
                }

                if (entry.Roles.Count > 0)
                {
                    var ar = await userManager.AddToRolesAsync(user, entry.Roles.Distinct(StringComparer.OrdinalIgnoreCase));
                    if (!ar.Succeeded)
                        logger.LogWarning("Seed roles for {Email} failed: {Errors}", email, string.Join("; ", ar.Errors.Select(e => e.Description)));
                }

                continue;
            }

            if (entry.Roles.Count > 0)
            {
                var current = await userManager.GetRolesAsync(existing);
                var missing = entry.Roles.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
                if (missing.Count > 0)
                {
                    var ar = await userManager.AddToRolesAsync(existing, missing);
                    if (!ar.Succeeded)
                        logger.LogWarning("Update seed roles for {Email} failed: {Errors}", email, string.Join("; ", ar.Errors.Select(e => e.Description)));
                }
            }

            // Keep seeded DepartmentId in sync for demo/dev uploads that require department context.
            if (existing.DepartmentId != entry.DepartmentId)
            {
                existing.DepartmentId = entry.DepartmentId;
                var updated = await userManager.UpdateAsync(existing);
                if (!updated.Succeeded)
                    logger.LogWarning("Update seed department for {Email} failed: {Errors}", email, string.Join("; ", updated.Errors.Select(e => e.Description)));
            }
        }
    }
}
