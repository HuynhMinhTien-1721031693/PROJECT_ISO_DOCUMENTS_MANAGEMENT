using System;
using IsoDoc.Domain.Common;
using IsoDoc.Domain.Enums;
using IsoDoc.Domain.ValueObjects;

namespace IsoDoc.Domain.Entities;

/// <summary>
/// Represents one physical file version of a Document.
/// Immutable after creation to support audit and tamper-evidence.
/// </summary>
public sealed class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; private set; }
    public string BlobPath { get; private set; } = string.Empty;
    public long FileSize { get; private set; }
    public DocumentFileType FileType { get; private set; }
    public FileChecksum Checksum { get; private set; } = null!;
    public string? ChangeNote { get; private set; }
    public Guid UploadedBy { get; private set; }
    public DateTime UploadedAt { get; private set; }
    public bool IsCurrentVersion { get; private set; }

    private DocumentVersion()
    {
        // For ORM
    }

    internal static DocumentVersion Create(
        Guid documentId,
        string blobPath,
        long fileSize,
        DocumentFileType fileType,
        FileChecksum checksum,
        Guid uploadedBy,
        string? changeNote = null)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("documentId is required.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(blobPath)) throw new ArgumentException("blobPath is required.", nameof(blobPath));
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize));
        if (uploadedBy == Guid.Empty) throw new ArgumentException("uploadedBy is required.", nameof(uploadedBy));

        return new DocumentVersion
        {
            DocumentId = documentId,
            BlobPath = blobPath.Trim(),
            FileSize = fileSize,
            FileType = fileType,
            Checksum = checksum,
            ChangeNote = string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim(),
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            IsCurrentVersion = false
        };
    }

    internal void SetAsCurrent() => IsCurrentVersion = true;
    internal void ClearCurrentFlag() => IsCurrentVersion = false;

    public bool VerifyIntegrity(byte[] fileContent) => Checksum.Verify(fileContent);

    public string FileSizeFormatted =>
        FileSize switch
        {
            < 1_024 => $"{FileSize} B",
            < 1_048_576 => $"{FileSize / 1_024.0:F1} KB",
            < 1_073_741_824 => $"{FileSize / 1_048_576.0:F1} MB",
            _ => $"{FileSize / 1_073_741_824.0:F1} GB"
        };
}

