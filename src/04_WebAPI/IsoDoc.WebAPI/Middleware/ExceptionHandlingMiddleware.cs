using System.Text.Json;
using FluentValidation;
using IsoDoc.Application.Common.Behaviours;
using IsoDoc.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace IsoDoc.WebAPI.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        #region agent log
        WriteDebugLog(
            "h7",
            "IsoDoc.WebAPI/Middleware/ExceptionHandlingMiddleware.cs:InvokeAsync:entry",
            "Incoming request",
            new
            {
                method = ctx.Request.Method,
                path = ctx.Request.Path.Value,
                query = ctx.Request.QueryString.Value,
                isAuthenticated = ctx.User?.Identity?.IsAuthenticated ?? false
            });
        #endregion
        try
        {
            await _next(ctx);
            #region agent log
            WriteDebugLog(
                "h8",
                "IsoDoc.WebAPI/Middleware/ExceptionHandlingMiddleware.cs:InvokeAsync:after-next",
                "Request pipeline completed",
                new
                {
                    method = ctx.Request.Method,
                    path = ctx.Request.Path.Value,
                    statusCode = ctx.Response.StatusCode
                });
            #endregion
        }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested)
        {
            ctx.Response.StatusCode = 499;
            #region agent log
            WriteDebugLog(
                "h9",
                "IsoDoc.WebAPI/Middleware/ExceptionHandlingMiddleware.cs:InvokeAsync:request-aborted",
                "Request aborted by client",
                new
                {
                    method = ctx.Request.Method,
                    path = ctx.Request.Path.Value,
                    statusCode = ctx.Response.StatusCode
                });
            #endregion
        }
        catch (Exception ex)
        {
            #region agent log
            WriteDebugLog(
                "h10",
                "IsoDoc.WebAPI/Middleware/ExceptionHandlingMiddleware.cs:InvokeAsync:exception",
                "Unhandled exception caught in middleware",
                new
                {
                    method = ctx.Request.Method,
                    path = ctx.Request.Path.Value,
                    exceptionType = ex.GetType().FullName,
                    exceptionMessage = ex.Message
                });
            #endregion
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var (statusCode, title, detail, errors) = ex switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Du lieu khong hop le",
                "Mot hoac nhieu truong du lieu khong dung dinh dang.",
                ve.Errors.GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),

            DocumentNotFoundException => (
                StatusCodes.Status404NotFound,
                "Khong tim thay tai lieu",
                ex.Message,
                (Dictionary<string, string[]>?)null),

            InvalidDocumentWorkflowStateException => (
                StatusCodes.Status422UnprocessableEntity,
                "Trang thai tai lieu khong hop le",
                ex.Message,
                (Dictionary<string, string[]>?)null),

            DomainException => (
                StatusCodes.Status422UnprocessableEntity,
                "Vi pham quy tac nghiep vu",
                ex.Message,
                (Dictionary<string, string[]>?)null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Chua xac thuc",
                "Ban can dang nhap de thuc hien thao tac nay.",
                (Dictionary<string, string[]>?)null),

            ForbiddenAccessException fax => (
                StatusCodes.Status403Forbidden,
                "Khong co quyen truy cap",
                $"Ban khong co quyen '{fax.Permission}'.",
                (Dictionary<string, string[]>?)null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Loi he thong",
                _env.IsDevelopment() ? ex.ToString() : "Da xay ra loi khong mong muon. Vui long thu lai.",
                (Dictionary<string, string[]>?)null)
        };

        if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        else
            _logger.LogWarning("Client error {StatusCode}: {Message}", statusCode, ex.Message);

        var problem = new ValidationProblemDetails
        {
            Type = $"https://isodms.internal/errors/{statusCode}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        problem.Extensions["traceId"] = ctx.TraceIdentifier;

        if (errors is not null)
            foreach (var kv in errors)
                problem.Errors[kv.Key] = kv.Value;

        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
        #region agent log
        WriteDebugLog(
            "h10",
            "IsoDoc.WebAPI/Middleware/ExceptionHandlingMiddleware.cs:HandleExceptionAsync:response",
            "ProblemDetails response written",
            new
            {
                method = ctx.Request.Method,
                path = ctx.Request.Path.Value,
                statusCode,
                title
            });
        #endregion
    }

    private static void WriteDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            const string debugLogPath = @"D:\HuynhMinhTien\CONG NGHE .NET\PROJECT_ISO_DOCUMENTS_MANAGEMENT\debug-b9f138.log";
            var payload = new
            {
                sessionId = "b9f138",
                runId = "pre-fix-2",
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
            // Keep middleware resilient during debug logging.
        }
    }
}
