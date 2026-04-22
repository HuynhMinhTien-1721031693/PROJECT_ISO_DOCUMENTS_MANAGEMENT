using IsoDoc.Application.Common.Configuration;
using IsoDoc.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Storage;

/// <summary>
/// Development / offline file storage under a folder on the host machine (no Azure account required).
/// </summary>
public sealed class LocalDiskBlobStorageService : IFileStorageService
{
    private readonly string _root;
    private readonly ILogger<LocalDiskBlobStorageService> _logger;

    public LocalDiskBlobStorageService(
        IOptions<BlobStorageOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<LocalDiskBlobStorageService> logger)
    {
        _logger = logger;
        var configured = options.Value.LocalDiskRootPath;
        _root = !string.IsNullOrWhiteSpace(configured)
            ? configured
            : Path.Combine(hostEnvironment.ContentRootPath, "App_Data", "blobs");
        Directory.CreateDirectory(_root);
    }

    public bool SupportsTimeLimitedPublicUrls => false;

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        CancellationToken ct = default)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var safeName = SanitiseFileName(fileName);
        var relative = $"{documentId}/{timestamp}_{safeName}";
        var fullPath = GetFullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        if (fileStream.CanSeek)
            fileStream.Position = 0;
        await fileStream.CopyToAsync(fs, ct);
        _logger.LogInformation("Uploaded local blob {Path}", relative);
        return relative.Replace('\\', '/');
    }

    public Task<string> GetSecureDownloadUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct = default)
        => throw new NotSupportedException("Local disk storage does not issue SAS URLs; use the authenticated file API.");

    public Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(blobPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Blob file not found.", fullPath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var fullPath = GetFullPath(blobPath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted local blob {Path}", blobPath);
        }

        return Task.CompletedTask;
    }

    private string GetFullPath(string blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
            throw new ArgumentException("Invalid blob path.", nameof(blobPath));

        var normalized = blobPath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            throw new ArgumentException("Invalid blob path.", nameof(blobPath));

        var rootFull = Path.GetFullPath(_root) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(_root, normalized));
        if (!fullPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(fullPath, Path.GetFullPath(_root), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid blob path.", nameof(blobPath));

        return fullPath;
    }

    private static string SanitiseFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return clean.Length > 100 ? clean[..100] : clean;
    }
}
