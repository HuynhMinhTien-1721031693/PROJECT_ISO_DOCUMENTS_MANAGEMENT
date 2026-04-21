using IsoDoc.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace IsoDoc.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddDbContext<AppDbContext>((_, options) =>
            options.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=IsoDocDmsMigrations;Trusted_Connection=True;MultipleActiveResultSets=true",
                sql => sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        return services.BuildServiceProvider().GetRequiredService<AppDbContext>();
    }
}
