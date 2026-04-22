using Azure.Storage.Blobs;
using Elastic.Clients.Elasticsearch;
using IsoDoc.Application.Common.Interfaces;
using IsoDoc.Domain.Interfaces;
using IsoDoc.Infrastructure.InMemory;
using IsoDoc.Infrastructure.Cache;
using IsoDoc.Infrastructure.Identity;
using IsoDoc.Infrastructure.Notifications;
using IsoDoc.Infrastructure.Persistence;
using IsoDoc.Infrastructure.Persistence.Repositories;
using IsoDoc.Infrastructure.Persistence.Identity;
using IsoDoc.Infrastructure.Search;
using IsoDoc.Application.Common.Configuration;
using IsoDoc.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;

namespace IsoDoc.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection")
            ?? configuration.GetConnectionString("SqlServer");
        #region agent log
        WriteDebugLog(
            "h1",
            "IsoDoc.Infrastructure/DependencyInjection.cs:AddInfrastructureServices:entry",
            "Evaluated DB connection strings",
            new
            {
                hasDefaultConnection = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")),
                hasSqlServerConnection = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("SqlServer")),
                selectedConnectionIsEmpty = string.IsNullOrWhiteSpace(defaultConnection)
            });
        #endregion

        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.Section));

        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(defaultConnection, sql =>
                {
                    sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sql.CommandTimeout(30);
                    sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                }));

            services.AddIdentityCore<ApplicationUser>(options =>
                {
                    options.User.RequireUniqueEmail = true;
                    options.Password.RequiredLength = 8;
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = true;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                    options.Lockout.MaxFailedAccessAttempts = 5;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<AppDbContext>();

            services.AddScoped<IUserAuthenticationService, IdentityUserAuthenticationService>();
            services.AddScoped<IUserAdministrationService, UserAdministrationService>();

            services.AddScoped<IDocumentRepository, DocumentRepository>();
            services.AddScoped<IApprovalWorkflowRepository, ApprovalWorkflowRepository>();
            services.AddScoped<IUserNotificationRepository, UserNotificationRepository>();
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IAuditLogReadRepository, AuditLogReadRepository>();
            services.AddScoped<IComplianceReportRepository, ComplianceReportingRepository>();
            #region agent log
            WriteDebugLog(
                "h2",
                "IsoDoc.Infrastructure/DependencyInjection.cs:AddInfrastructureServices:sql-branch",
                "Registered SQL-backed repositories",
                new { repositoryMode = "sql" });
            #endregion
        }
        else
        {
            services.AddScoped<IUserAuthenticationService, ConfigFileUserAuthenticationService>();
            services.AddScoped<IUserAdministrationService, UnsupportedUserAdministrationService>();

            // Local fallback so the API can start without a database connection string.
            services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
            services.AddSingleton<IApprovalWorkflowRepository, InMemoryApprovalWorkflowRepository>();
            services.AddSingleton<IUserNotificationRepository, InMemoryUserNotificationRepository>();
            services.AddSingleton<InMemoryAuditLogStore>();
            services.AddSingleton<IAuditLogReadRepository, InMemoryAuditLogReadRepository>();
            services.AddScoped<IAuditService, InMemoryBufferedAuditService>();
            services.AddScoped<IComplianceReportRepository, InMemoryComplianceReportRepository>();
            #region agent log
            WriteDebugLog(
                "h2",
                "IsoDoc.Infrastructure/DependencyInjection.cs:AddInfrastructureServices:inmemory-branch",
                "Registered in-memory repositories",
                new { repositoryMode = "inmemory" });
            #endregion
        }

        services.Configure<BlobStorageOptions>(configuration.GetSection(BlobStorageOptions.Section));
        var blobOptions = configuration.GetSection(BlobStorageOptions.Section).Get<BlobStorageOptions>() ?? new BlobStorageOptions();
        var useLocalDisk = blobOptions.UseLocalDisk || string.IsNullOrWhiteSpace(blobOptions.ConnectionString);
        if (useLocalDisk)
        {
            services.AddScoped<IFileStorageService, Storage.LocalDiskBlobStorageService>();
        }
        else
        {
            services.AddSingleton(_ => new BlobServiceClient(blobOptions.ConnectionString));
            services.AddScoped<IFileStorageService, Storage.AzureBlobStorageService>();
        }

        services.Configure<ElasticsearchOptions>(configuration.GetSection(ElasticsearchOptions.Section));
        services.AddSingleton(sp =>
        {
            var options = configuration.GetSection(ElasticsearchOptions.Section).Get<ElasticsearchOptions>() ?? new ElasticsearchOptions();
            var settings = new ElasticsearchClientSettings(new Uri(options.Uri));
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                settings.Authentication(new Elastic.Transport.ApiKey(options.ApiKey));
            return new ElasticsearchClient(settings);
        });
        services.AddScoped<ElasticsearchService>();
        services.AddScoped<ISearchService, ResilientSearchService>();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddSingleton<InMemory.InMemoryCacheService>();
            services.AddScoped<ICacheService>(sp => sp.GetRequiredService<InMemory.InMemoryCacheService>());
            services.AddScoped<IRedisCacheService>(sp => sp.GetRequiredService<InMemory.InMemoryCacheService>());
        }
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(_ =>
            {
                var options = ConfigurationOptions.Parse(redisConnection);
                options.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(options);
            });
            services.AddScoped<Cache.RedisCacheService>();
            services.AddScoped<ICacheService>(sp => sp.GetRequiredService<Cache.RedisCacheService>());
            services.AddScoped<IRedisCacheService>(sp => sp.GetRequiredService<Cache.RedisCacheService>());
        }

        var jwtSection = configuration.GetSection(JwtOptions.Section);
        if (!jwtSection.Exists())
            jwtSection = configuration.GetSection("IsoDoc:Jwt");
        services.Configure<JwtOptions>(jwtSection);
        services.Configure<ApproverResolverOptions>(configuration.GetSection(ApproverResolverOptions.Section));
        services.AddHttpContextAccessor();
        services.AddSingleton<IJwtTokenService, Identity.JwtTokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IApproverResolverService, ApproverResolverService>();
        services.AddSingleton<IUserDirectoryLookup, ConfigAuthUserDirectoryService>();

        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.Section));
        services.AddScoped<INotificationSender, NotificationService>();
        services.AddScoped<INotificationService, DomainNotificationService>();
        #region agent log
        WriteDebugLog(
            "h3",
            "IsoDoc.Infrastructure/DependencyInjection.cs:AddInfrastructureServices:exit",
            "Infrastructure registration completed",
            new { finished = true });
        #endregion

        return services;
    }

    public static IServiceCollection AddIsoDocInfrastructure(this IServiceCollection services, IConfiguration configuration)
        => services.AddInfrastructureServices(configuration);

    public static async Task ApplyMigrationsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
            return;
        await db.Database.MigrateAsync();
        await IdentityDataSeeder.SeedAsync(services);
    }

    private static void WriteDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            const string debugLogPath = @"D:\HuynhMinhTien\CONG NGHE .NET\PROJECT_ISO_DOCUMENTS_MANAGEMENT\debug-b9f138.log";
            var payload = new
            {
                sessionId = "b9f138",
                runId = "pre-fix-1",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            File.AppendAllText(
                debugLogPath,
                JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // Keep startup resilient during debug logging.
        }
    }
}
