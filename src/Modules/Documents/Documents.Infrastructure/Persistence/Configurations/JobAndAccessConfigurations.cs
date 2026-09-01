using Documents.Domain.Aggregates;
using Documents.Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Documents.Infrastructure.Persistence.Configurations;

public sealed class DocumentProcessingJobConfiguration : IEntityTypeConfiguration<DocumentProcessingJob>
{
    public void Configure(EntityTypeBuilder<DocumentProcessingJob> b)
    {
        b.ToTable("document_processing_jobs", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentProcessingJobId(v));
        b.Property(x => x.DocumentId).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentId(v));
        b.Property(x => x.DocumentVersionId).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentVersionId(v));
        b.HasIndex(x => x.DocumentId);
        b.Property(x => x.StageStatesJson).HasColumnName("StageStatusesJson").HasColumnType("jsonb");
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Ignore(x => x.StageStates);
        b.Ignore(x => x.StageStatesJson);
    }
}

public sealed class DocumentAccessEntryConfiguration : IEntityTypeConfiguration<DocumentAccessEntry>
{
    public void Configure(EntityTypeBuilder<DocumentAccessEntry> b)
    {
        b.ToTable("document_access_entries", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentId).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentId(v));
        b.HasIndex(x => new { x.DocumentId, x.TenantId });
        b.HasIndex(x => x.Timestamp);
        b.Property(x => x.Action).IsRequired().HasMaxLength(20);
        b.Property(x => x.ClassificationValue).IsRequired().HasMaxLength(100);
        b.Property(x => x.RuleVersion).HasMaxLength(20);
        b.Property(x => x.Reason).HasMaxLength(200);
    }
}

public sealed class DocumentExplicitGrantConfiguration : IEntityTypeConfiguration<DocumentExplicitGrant>
{
    public void Configure(EntityTypeBuilder<DocumentExplicitGrant> b)
    {
        b.ToTable("document_explicit_grants", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentId).HasConversion(id => id.Value, v => new Documents.Domain.Ids.DocumentId(v));
        b.HasIndex(x => new { x.DocumentId, x.GranteeUserId }).IsUnique();
    }
}

public sealed class ClassificationRuleConfiguration : IEntityTypeConfiguration<ClassificationRule>
{
    public void Configure(EntityTypeBuilder<ClassificationRule> b)
    {
        b.ToTable("classification_rules", "documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Version).IsRequired().HasMaxLength(20);
        b.Property(x => x.RuleSetJson).HasColumnType("jsonb");
        b.HasIndex(x => new { x.OrganizationId, x.Version }).IsUnique();
    }
}

public sealed class ClassificationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public string Version { get; set; } = "v1";
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public bool IsCurrent { get; set; }
    public string RuleSetJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
}
