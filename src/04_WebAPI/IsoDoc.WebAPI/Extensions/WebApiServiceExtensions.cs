using System.Text;
using System.Threading.RateLimiting;
using IsoDoc.Infrastructure.Identity;
using IsoDoc.Infrastructure.Notifications;
using IsoDoc.Infrastructure.Search;
using IsoDoc.WebAPI.Middleware;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace IsoDoc.WebAPI.Extensions;

public static class WebApiServiceExtensions
{
    public static IServiceCollection AddWebApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers(opts => { opts.ReturnHttpNotAcceptable = true; })
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter());
                opts.JsonSerializerOptions.DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ISO Document Management System API",
                Version = "v1",
                Description = "API quan ly tai lieu theo tieu chuan ISO 9001, 45001, 27001."
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhap JWT token. Vi du: Bearer eyJhbGc..."
            };
            c.AddSecurityDefinition("Bearer", jwtScheme);
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var jwtOpts = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>()
            ?? configuration.GetSection("IsoDoc:Jwt").Get<JwtOptions>()
            ?? new JwtOptions();
        var key = Encoding.UTF8.GetBytes(jwtOpts.SecretKey);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                opts.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddSingleton<IUserIdProvider, SignalRUserIdProvider>();
        services.AddSignalR();

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireController", p => p.RequireRole("DocumentController", "ISOManager", "SystemAdmin"))
            .AddPolicy(
                "RequireApprover",
                p => p.RequireRole("QAOfficer", "SafetyOfficer", "ISMSOfficer", "ISOManager", "SystemAdmin"))
            .AddPolicy("RequireISOManager", p => p.RequireRole("ISOManager", "SystemAdmin"))
            .AddPolicy("RequireSystemAdmin", p => p.RequireRole("SystemAdmin"))
            .AddPolicy("RequireViewer", p => p.RequireAuthenticatedUser());

        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:5000", "https://localhost:5001" };

        services.AddCors(opts =>
            opts.AddPolicy("DefaultCors", policy =>
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials()));

        var healthChecks = services.AddHealthChecks();
        if (configuration.GetSection(ElasticsearchOptions.Section).GetValue("ParticipateInHealthChecks", false))
        {
            healthChecks.AddCheck<ElasticsearchHealthCheck>(
                "elasticsearch",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "elasticsearch", "ready" });
        }

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("login", httpContext =>
            {
                var env = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
                var permitLimit = string.Equals(
                        env.EnvironmentName,
                        "IntegrationTests",
                        StringComparison.OrdinalIgnoreCase)
                    ? 10_000
                    : configuration.GetValue("RateLimiting:LoginPermitPerMinute", 40);
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }

    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<ExceptionHandlingMiddleware>();
}
