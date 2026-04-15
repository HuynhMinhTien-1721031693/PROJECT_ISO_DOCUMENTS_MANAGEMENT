using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using IsoDoc.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IsoDoc.Infrastructure.Storage;

public sealed class AzureBlobStorageService : IFileStorageService
{
    private readonly BlobServiceClient _blobService;
    private readonly BlobStorageOptions _options;
    private readonly ILogger<AzureBlobStorageService> _logger;

    public AzureBlobStorageService(
        BlobServiceClient blobService,
        IOptions<BlobStorageOptions> options,
        ILogger<AzureBlobStorageService> logger)
    {
        _blobService = blobService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid documentId,
        CancellationToken ct = default)
    {
        var container = _blobService.GetBlobContainerClient(_options.ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var safeName = SanitiseFileName(fileName);
        var blobPath = $"{documentId}/{timestamp}_{safeName}";
        var blobClient = container.GetBlobClient(blobPath);

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = new Dictionary<string, string>
            {
                ["documentId"] = documentId.ToString(),
                ["originalName"] = fileName,
                ["uploadedAt"] = DateTime.UtcNow.ToString("O")
            }
        };

        await blobClient.UploadAsync(fileStream, uploadOptions, ct);
        _logger.LogInformation("Uploaded blob {BlobPath}", blobPath);
        return blobPath;
    }

    public Task<string> GetSecureDownloadUrlAsync(string blobPath, TimeSpan expiry, CancellationToken ct = default)
    {
        var container = _blobService.GetBlobContainerClient(_options.ContainerName);
        var blobClient = container.GetBlobClient(blobPath);
        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException("BlobClient cannot generate SAS URI.");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _options.ContainerName,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var container = _blobService.GetBlobContainerClient(_options.ContainerName);
        var blobClient = container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
        _logger.LogInformation("Deleted blob {BlobPath}", blobPath);
    }

    private static string SanitiseFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(fileName.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return clean.Length > 100 ? clean[..100] : clean;
    }
}

public sealed class BlobStorageOptions
{
    public const string Section = "BlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "iso-documents";
    public int SasExpiryHours { get; set; } = 1;
}
