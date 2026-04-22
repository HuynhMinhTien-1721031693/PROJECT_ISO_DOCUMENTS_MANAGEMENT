using System.Diagnostics;
using IsoDoc.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Common.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public LoggingBehaviour(
        ILogger<LoggingBehaviour<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUser.UserId?.ToString() ?? "anonymous";

        _logger.LogInformation("Handling {RequestName} | User: {UserId}", requestName, userId);

        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "SLOW REQUEST: {RequestName} took {ElapsedMs}ms | User: {UserId}",
                    requestName, sw.ElapsedMilliseconds, userId);
            }
            else
            {
                _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Error handling {RequestName} after {ElapsedMs}ms | User: {UserId}",
                requestName, sw.ElapsedMilliseconds, userId);
            throw;
        }
    }
}

