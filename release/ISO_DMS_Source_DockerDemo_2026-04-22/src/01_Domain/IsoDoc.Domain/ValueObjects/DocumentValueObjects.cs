using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace IsoDoc.Domain.ValueObjects;

/// <summary>
/// Strongly-typed document code enforcing format: [PREFIX]-[TYPE]-[NUMBER]
/// Examples: QMS-PR-001, ISMS-POL-003, OHS-WI-012
/// </summary>
public sealed class DocumentCode : IEquatable<DocumentCode>
{
    private static readonly Regex _format =
        new(@"^[A-Z]{2,6}-[A-Z]{2,4}-\d{3}$", RegexOptions.Compiled);

    public string Value { get; }

    private DocumentCode(string value) => Value = value;

    public static DocumentCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Document code cannot be empty.", nameof(value));

        var normalized = value.Trim().ToUpperInvariant();
        if (!_format.IsMatch(normalized))
            throw new ArgumentException(
                $"Document code '{value}' must match format: PREFIX-TYPE-NNN (e.g. QMS-PR-001).",
                nameof(value));

        return new DocumentCode(normalized);
    }

    public override string ToString() => Value;

    public bool Equals(DocumentCode? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is DocumentCode dc && Equals(dc);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public static implicit operator string(DocumentCode code) => code.Value;
}

/// <summary>
/// Semantic version number for documents.
/// Major version increments on structural changes, minor on non-structural edits.
/// Format: MAJOR.MINOR
/// </summary>
public sealed class VersionNumber : IEquatable<VersionNumber>, IComparable<VersionNumber>
{
    public int Major { get; }
    public int Minor { get; }

    private VersionNumber(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    public static VersionNumber Initial => new(1, 0);

    public static VersionNumber Create(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Version cannot be empty.", nameof(version));

        var parts = version.Split('.');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var major) || major < 1
            || !int.TryParse(parts[1], out var minor) || minor < 0)
        {
            throw new ArgumentException(
                $"Version '{version}' must be in format MAJOR.MINOR (e.g. 1.0, 2.3).",
                nameof(version));
        }

        return new VersionNumber(major, minor);
    }

    /// <summary>Bumps minor: 1.2 → 1.3</summary>
    public VersionNumber BumpMinor() => new(Major, Minor + 1);

    /// <summary>Bumps major and resets minor: 1.2 → 2.0</summary>
    public VersionNumber BumpMajor() => new(Major + 1, 0);

    public override string ToString() => $"{Major}.{Minor}";

    public bool Equals(VersionNumber? other) => other is not null && Major == other.Major && Minor == other.Minor;
    public override bool Equals(object? obj) => obj is VersionNumber v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(Major, Minor);

    public int CompareTo(VersionNumber? other)
    {
        if (other is null) return 1;
        var majorComp = Major.CompareTo(other.Major);
        return majorComp != 0 ? majorComp : Minor.CompareTo(other.Minor);
    }

    public static implicit operator string(VersionNumber v) => v.ToString();
}

/// <summary>
/// SHA-256 checksum for file integrity verification (ISO 27001 requirement).
/// </summary>
public sealed class FileChecksum : IEquatable<FileChecksum>
{
    public string Value { get; }

    private FileChecksum(string value) => Value = value;

    public static FileChecksum FromBytes(byte[] fileContent)
    {
        ArgumentNullException.ThrowIfNull(fileContent);
        var hash = SHA256.HashData(fileContent);
        return new FileChecksum(Convert.ToHexString(hash).ToLowerInvariant());
    }

    public static FileChecksum FromHexString(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length != 64)
            throw new ArgumentException("Checksum must be a 64-character hex string.", nameof(hex));
        return new FileChecksum(hex.ToLowerInvariant());
    }

    public bool Verify(byte[] fileContent)
        => Value == Convert.ToHexString(SHA256.HashData(fileContent)).ToLowerInvariant();

    public override string ToString() => Value;

    public bool Equals(FileChecksum? other) => other is not null && Value == other.Value;
    public override bool Equals(object? obj) => obj is FileChecksum fc && Equals(fc);
    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);
}

