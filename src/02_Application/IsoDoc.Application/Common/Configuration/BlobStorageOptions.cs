namespace IsoDoc.Application.Common.Configuration;

public sealed class BlobStorageOptions
{
    public const string Section = "BlobStorage";
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "iso-documents";
    public int SasExpiryHours { get; set; } = 1;

    /// <summary>When true or when <see cref="ConnectionString"/> is empty, files are stored on local disk (dev / air-gapped).</summary>
    public bool UseLocalDisk { get; set; }

    /// <summary>Root directory for local disk storage. Defaults to App_Data/blobs under the host content root.</summary>
    public string? LocalDiskRootPath { get; set; }
}
