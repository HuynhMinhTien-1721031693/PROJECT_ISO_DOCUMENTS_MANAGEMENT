using System.ComponentModel.DataAnnotations;

namespace IsoDoc.Blazor.Models;

public sealed class UploadFormModel
{
    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string DocumentCode { get; set; } = "QMS-PR-001";

    [Required]
    public string Standard { get; set; } = "ISO9001";

    [Required]
    public string Category { get; set; } = "Procedure";

    public string? Description { get; set; }
    public string? ChangeNote { get; set; } = "Initial release";
    public string TagsRaw { get; set; } = string.Empty;
}
