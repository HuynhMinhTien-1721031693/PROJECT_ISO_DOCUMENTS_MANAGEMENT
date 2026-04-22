namespace IsoDoc.Application.Common;

public static class DocumentFileUploadRules
{
    public static readonly string[] AllowedContentTypes =
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public const long MaxFileSizeBytes = 50 * 1024 * 1024;
}
