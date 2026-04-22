namespace IsoDoc.Blazor.Models;

public sealed class UserListItemDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public Guid? DepartmentId { get; init; }
    public bool LockoutEnabled { get; init; }
    public bool LockedOut { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
}
