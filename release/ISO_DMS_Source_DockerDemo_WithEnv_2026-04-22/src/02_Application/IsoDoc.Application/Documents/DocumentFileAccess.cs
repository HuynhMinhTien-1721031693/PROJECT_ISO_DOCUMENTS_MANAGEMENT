using IsoDoc.Domain.Entities;

namespace IsoDoc.Application.Documents;

internal static class DocumentFileAccess
{
    public static DocumentVersion? ResolveVersion(Document document, Guid? versionId)
    {
        if (versionId is null || versionId == Guid.Empty)
            return document.Versions.OrderByDescending(v => v.UploadedAt).FirstOrDefault();

        return document.Versions.FirstOrDefault(v => v.Id == versionId);
    }
}
