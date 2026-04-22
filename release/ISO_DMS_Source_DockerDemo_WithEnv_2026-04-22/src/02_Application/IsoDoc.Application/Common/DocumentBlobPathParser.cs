namespace IsoDoc.Application.Common;

public static class DocumentBlobPathParser
{
    public static string OriginalFileName(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            return "download";

        var last = blobPath.Replace('\\', '/').Split('/').LastOrDefault() ?? "download";
        var underscore = last.IndexOf('_');
        return underscore >= 0 && underscore < last.Length - 1
            ? last[(underscore + 1)..]
            : last;
    }
}
