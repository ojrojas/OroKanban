using Documents.Domain.Aggregates;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("documents", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentId(v));
        b.Property(x => x.Name).IsRequired().HasMaxLength(300);
        b.Property(x => x.ClassificationValue).IsRequired().HasMaxLength(100);
        b.Property(x => x.ClassificationLevelId).IsRequired();
        b.Property(x => x.RuleVersion).IsRequired().HasMaxLength(20);
        b.Property(x => x.CurrentVersionId).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentVersionId(v));
        b.Property(x => x.MimeType).IsRequired().HasMaxLength(255);
        b.Property(x => x.ContentHash).IsRequired().HasMaxLength(64);
        b.HasIndex(x => x.ContentHash);
        b.HasIndex(x => new { x.TenantId, x.ProjectId });
        b.HasIndex(x => new { x.TenantId, x.OwnerId });
        b.Property(x => x.Status).HasConversion(s => s.Id, id => Documents.Domain.Enumerations.DocumentStatus.FromId(id));
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Property(x => x.TenantId).IsRequired();
        b.Property(x => x.OrganizationId).IsRequired();
        b.Property(x => x.OwnerId).IsRequired();
    }
}
