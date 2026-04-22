using IsoDoc.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IsoDoc.Application.Common.Behaviours;

public sealed class AuthorizationBehaviour<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionService _permissions;
    private readonly ILogger<AuthorizationBehaviour<TRequest, TResponse>> _logger;

    public AuthorizationBehaviour(
        ICurrentUserService currentUser,
        IPermissionService permissions,
        ILogger<AuthorizationBehaviour<TRequest, TResponse>> logger)
    {
        _currentUser = currentUser;
        _permissions = permissions;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var authorizeAttrs = typeof(TRequest)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        if (!authorizeAttrs.Any()) return await next();

        if (_currentUser.UserId is null)
            throw new UnauthorizedAccessException("User is not authenticated.");

        foreach (var attr in authorizeAttrs.Where(a => !string.IsNullOrEmpty(a.Permission)))
        {
            var hasPermission = await _permissions.HasPermissionAsync(
                _currentUser.UserId.Value, attr.Permission!, cancellationToken);

            if (!hasPermission)
            {
                _logger.LogWarning(
                    "Authorization failed: User {UserId} missing permission '{Permission}' for {Request}",
                    _currentUser.UserId, attr.Permission, typeof(TRequest).Name);
                throw new ForbiddenAccessException(_currentUser.UserId.Value, attr.Permission!);
            }
        }

        return await next();
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class AuthorizeAttribute : Attribute
{
    public string? Permission { get; set; }
    public string? Role { get; set; }
}

public sealed class ForbiddenAccessException : Exception
{
    public Guid UserId { get; }
    public string Permission { get; }

    public ForbiddenAccessException(Guid userId, string permission)
        : base($"User '{userId}' does not have permission '{permission}'.")
    {
        UserId = userId;
        Permission = permission;
    }
}

