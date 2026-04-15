using System.Text.Json;
using IsoDoc.Domain.Entities;
using IsoDoc.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IsoDoc.Infrastructure.Persistence.Configurations;

public sealed class DocumentEntityConfiguration : IEntityTypeConfiguration<Document>
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Title).HasMaxLength(500).IsRequired();

        builder.Property(d => d.Code)
            .HasColumnName("DocumentCode")
            .HasMaxLength(50)
            .IsRequired()
            .HasConversion(c => c.Value, v => DocumentCode.Create(v));

        builder.HasIndex(d => d.Code).IsUnique();

        builder.Property(d => d.CurrentVersion)
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(v => v.ToString(), s => VersionNumber.Create(s));

        builder.Property(d => d.Standard).HasConversion<int>();
        builder.Property(d => d.Category).HasConversion<int>();
        builder.Property(d => d.Status).HasConversion<int>();
        builder.Property(d => d.Description).HasMaxLength(4000);

        var comparer = new ValueComparer<IReadOnlyList<string>>(
            (a, b) => (a ?? Array.Empty<string>()).SequenceEqual(b ?? Array.Empty<string>()),
            c => c.Aggregate(0, (h, x) => HashCode.Combine(h, x.GetHashCode())),
            c => c.ToList().AsReadOnly());

        builder.Property(d => d.Tags)
            .HasMaxLength(4000)
            .HasConversion(new TagsConverter())
            .Metadata.SetValueComparer(comparer);

        builder.HasMany<DocumentVersion>("_versions")
            .WithOne()
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }

    private sealed class TagsConverter : ValueConverter<IReadOnlyList<string>, string>
    {
        public TagsConverter()
            : base(
                v => JsonSerializer.Serialize(v ?? Array.Empty<string>(), JsonOptions),
                v => Deserialize(v))
        {
        }

        private static IReadOnlyList<string> Deserialize(string json)
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            return (list ?? new List<string>()).AsReadOnly();
        }
    }
}

public sealed class DocumentVersionEntityConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("DocumentVersions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.BlobPath).HasMaxLength(1000).IsRequired();
        builder.Property(v => v.FileSize).IsRequired();
        builder.Property(v => v.ChangeNote).HasMaxLength(2000);
        builder.Property(v => v.FileType).HasConversion<int>().IsRequired();

        builder.Property(v => v.Checksum)
            .HasMaxLength(64)
            .IsRequired()
            .HasConversion(c => c.Value, s => FileChecksum.FromHexString(s));
    }
}

public sealed class ApprovalWorkflowEntityConfiguration : IEntityTypeConfiguration<ApprovalWorkflow>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow> builder)
    {
        builder.ToTable("ApprovalWorkflows");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Status).HasConversion<int>().IsRequired();

        builder.HasMany<ApprovalWorkflow.ApprovalStep>("_steps")
            .WithOne()
            .HasForeignKey(s => s.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation("_steps").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ApprovalStepEntityConfiguration : IEntityTypeConfiguration<ApprovalWorkflow.ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalWorkflow.ApprovalStep> builder)
    {
        builder.ToTable("ApprovalSteps");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Decision).HasConversion<int>().IsRequired();
        builder.Property(s => s.Comment).HasMaxLength(2000);
    }
}

public sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityType).HasMaxLength(100);
        builder.Property(a => a.EntityId).HasMaxLength(200);
        builder.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.IpAddress).HasMaxLength(45);
        builder.Property(a => a.UserAgent).HasMaxLength(500);
        builder.HasIndex(a => a.OccurredAt);
    }
}
