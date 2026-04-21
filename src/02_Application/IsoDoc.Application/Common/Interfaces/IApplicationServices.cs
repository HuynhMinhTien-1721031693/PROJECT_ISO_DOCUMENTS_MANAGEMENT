using IsoDoc.Domain.Enums;

namespace IsoDoc.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid DepartmentId { get; }
    string? Email { get; }
    string? FullName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
}

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(Guid userId, CancellationToken ct = default);
}

public interface INotificationSender
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
    Task SendInAppNotificationAsync(Guid userId, string title, string message, string? actionUrl = null, CancellationToken ct = default);
}

/// <summary>Resolves user contact info from the configured identity store (intranet / dev auth users).</summary>
public interface IUserDirectoryLookup
{
    string? TryGetEmail(Guid userId);
    string? TryGetDisplayName(Guid userId);
}

public interface IApproverResolverService
{
    Task<(Guid Step1ApproverId, Guid Step2ApproverId)> ResolveAsync(IsoStandard standard, CancellationToken ct = default);
}

public static class Permissions
{
    public const string DocumentUpload = "document:upload";
    public const string DocumentEdit = "document:edit";
    public const string DocumentDelete = "document:delete";
    public const string DocumentSubmit = "document:submit";
    public const string DocumentApprove = "document:approve";
    public const string DocumentArchive = "document:archive";
    public const string DocumentViewAll = "document:view_all";
    public const string WorkflowViewPending = "workflow:view_pending";
    public const string UserManage = "user:manage";
    public const string RoleAssign = "role:assign";
    public const string AuditLogView = "audit:view";
    public const string ComplianceReportView = "compliance:view";
}

