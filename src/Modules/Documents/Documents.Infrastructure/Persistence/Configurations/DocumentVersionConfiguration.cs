using Documents.Domain.Aggregates;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> b)
    {
        b.ToTable("document_versions", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.Ids.DocumentVersionId(v));
        b.Property(x => x.DocumentId).HasConversion(id => id.Value, v => new Domain.Ids.DocumentId(v));
        b.HasIndex(x => new { x.DocumentId, x.VersionNumber }).IsUnique();
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        b.Property(x => x.MimeType).IsRequired().HasMaxLength(255);
        b.Property(x => x.RuleVersion).IsRequired().HasMaxLength(20);
        b.Property(x => x.MetadataSnapshotJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.ScanStatusId).IsRequired();
        b.HasIndex(x => x.IsSafe);
        b.Property(x => x.RowVersion).IsRowVersion();
    }
}
