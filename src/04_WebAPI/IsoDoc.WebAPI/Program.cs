using IsoDoc.Application;
using IsoDoc.Infrastructure;
using IsoDoc.WebAPI.Extensions;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting ISO Document Management System v3...");

    var builder = WebApplication.CreateBuilder(args);
    #region agent log
    WriteDebugLog(
        "h4",
        "IsoDoc.WebAPI/Program.cs:startup:builder-created",
        "WebApplicationBuilder created",
        new
        {
            environment = builder.Environment.EnvironmentName,
            appName = builder.Environment.ApplicationName
        });
    #endregion

    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext());

    builder.Services
        .AddApplicationServices()
        .AddInfrastructureServices(builder.Configuration)
        .AddWebApiServices(builder.Configuration);
    #region agent log
    WriteDebugLog(
        "h5",
        "IsoDoc.WebAPI/Program.cs:startup:services-registered",
        "Core service registration completed",
        new { stage = "post-add-services" });
    #endregion

    builder.Services.Configure<ApiBehaviorOptions>(options =>
        options.SuppressModelStateInvalidFilter = true);

    #region agent log
    WriteDebugLog(
        "h11",
        "IsoDoc.WebAPI/Program.cs:startup:before-build",
        "Starting app builder.Build",
        new { stage = "before-build" });
    #endregion
    var app = builder.Build();
    #region agent log
    WriteDebugLog(
        "h11",
        "IsoDoc.WebAPI/Program.cs:startup:after-build",
        "Completed app builder.Build",
        new { stage = "after-build" });
    #endregion

    app.UseExceptionHandling();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ISO DMS API v1");
            c.RoutePrefix = "swagger";
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("DefaultCors");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    if (app.Environment.IsDevelopment())
    {
        #region agent log
        WriteDebugLog(
            "h12",
            "IsoDoc.WebAPI/Program.cs:startup:before-migrate",
            "Starting ApplyMigrationsAsync",
            new { stage = "before-migrate", env = app.Environment.EnvironmentName });
        #endregion
        await IsoDoc.Infrastructure.DependencyInjection.ApplyMigrationsAsync(app.Services);
        #region agent log
        WriteDebugLog(
            "h12",
            "IsoDoc.WebAPI/Program.cs:startup:after-migrate",
            "Completed ApplyMigrationsAsync",
            new { stage = "after-migrate", env = app.Environment.EnvironmentName });
        #endregion
    }

    #region agent log
    WriteDebugLog(
        "h13",
        "IsoDoc.WebAPI/Program.cs:startup:before-run",
        "Starting app.RunAsync",
        new { stage = "before-run" });
    #endregion
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    #region agent log
    WriteDebugLog(
        "h5",
        "IsoDoc.WebAPI/Program.cs:startup:exception",
        "Startup exception captured",
        new { exceptionType = ex.GetType().FullName, exceptionMessage = ex.Message });
    #endregion
    Log.Fatal(ex, "Application terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

static void WriteDebugLog(string hypothesisId, string location, string message, object data)
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
