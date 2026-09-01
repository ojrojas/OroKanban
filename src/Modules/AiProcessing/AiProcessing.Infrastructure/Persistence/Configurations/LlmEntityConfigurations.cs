using AiProcessing.Domain.Aggregates;
using AiProcessing.Domain.Ids;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AiProcessing.Infrastructure.Persistence.Configurations;

public sealed class LlmOperationConfiguration : IEntityTypeConfiguration<LlmOperation>
{
    public void Configure(EntityTypeBuilder<LlmOperation> b)
    {
        b.ToTable("llm_operations", "ai_processing");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new LlmOperationId(v));
        b.HasIndex(x => new { x.TenantId, x.DocumentId });
        b.HasIndex(x => new { x.TenantId, x.OperationStatusId });
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.Property(x => x.StageStatusesJson).HasColumnType("jsonb");
    }
}

public sealed class LlmPromptVersionConfiguration : IEntityTypeConfiguration<LlmPromptVersion>
{
    public void Configure(EntityTypeBuilder<LlmPromptVersion> b)
    {
        b.ToTable("llm_prompt_versions", "ai_processing");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new LlmPromptVersionId(v));
        b.HasIndex(x => new { x.OperationTypeId, x.VersionNumber }).IsUnique();
        b.HasIndex(x => new { x.OperationTypeId, x.IsPublished });
        b.Property(x => x.Template).IsRequired().HasMaxLength(20000);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}

public sealed class LlmResultConfiguration : IEntityTypeConfiguration<LlmResult>
{
    public void Configure(EntityTypeBuilder<LlmResult> b)
    {
        b.ToTable("llm_results", "ai_processing");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new LlmResultId(v));
        b.Property(x => x.OperationId).HasConversion(id => id.Value, v => new LlmOperationId(v));
        b.HasIndex(x => x.OperationId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.DocumentId });
        b.HasIndex(x => new { x.TenantId, x.ReviewStatusId });
        b.Property(x => x.ProvenanceJson).IsRequired().HasColumnType("jsonb");
        b.Property(x => x.Content).IsRequired().HasMaxLength(50000);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}

public sealed class LlmReviewConfiguration : IEntityTypeConfiguration<LlmReview>
{
    public void Configure(EntityTypeBuilder<LlmReview> b)
    {
        b.ToTable("llm_reviews", "ai_processing");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new LlmReviewId(v));
        b.Property(x => x.ResultId).HasConversion(id => id.Value, v => new LlmResultId(v));
        b.HasIndex(x => x.ResultId).IsUnique();
        b.Property(x => x.Rationale).IsRequired().HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}

public sealed class ChunkReferenceConfiguration : IEntityTypeConfiguration<ChunkReferenceEntity>
{
    public void Configure(EntityTypeBuilder<ChunkReferenceEntity> b)
    {
        b.ToTable("chunk_references", "ai_processing");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.DocumentId });
        b.HasIndex(x => new { x.TenantId, x.Classification });
        b.Property(x => x.IsSafe).IsRequired();
    }
}

public sealed class ReviewPolicyConfiguration : IEntityTypeConfiguration<ReviewPolicyEntity>
{
    public void Configure(EntityTypeBuilder<ReviewPolicyEntity> b)
    {
        b.ToTable("review_policies", "ai_processing");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.TenantId, x.OperationTypeId, x.Classification }).IsUnique();
        b.Property(x => x.RequiresReview).IsRequired();
    }
}

// Placeholder entities for EF configuration (real aggregates already exist)
public sealed class ChunkReferenceEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid DocumentVersionId { get; set; }
    public int ChunkId { get; set; }
    public string Classification { get; set; } = default!;
    public bool IsSafe { get; set; }
    public bool IsCurrentVersion { get; set; }
    public Guid? ProjectId { get; set; }
}

public sealed class ReviewPolicyEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public int OperationTypeId { get; set; }
    public string Classification { get; set; } = default!;
    public bool RequiresReview { get; set; }
    public bool IsCurrent { get; set; } = true;
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
}
