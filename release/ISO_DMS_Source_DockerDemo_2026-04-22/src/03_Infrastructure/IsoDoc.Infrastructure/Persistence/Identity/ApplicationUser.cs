using Microsoft.AspNetCore.Identity;

namespace IsoDoc.Infrastructure.Persistence.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public Guid? DepartmentId { get; set; }
}
