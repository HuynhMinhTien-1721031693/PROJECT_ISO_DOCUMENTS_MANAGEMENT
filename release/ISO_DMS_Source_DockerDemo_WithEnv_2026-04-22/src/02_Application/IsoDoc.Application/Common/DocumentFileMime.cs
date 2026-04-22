using IsoDoc.Domain.Enums;

namespace IsoDoc.Application.Common;

public static class DocumentFileMime
{
    public static string ForFileType(DocumentFileType fileType) => fileType switch
    {
        DocumentFileType.Pdf => "application/pdf",
        DocumentFileType.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        DocumentFileType.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        DocumentFileType.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        _ => "application/octet-stream"
    };
}
