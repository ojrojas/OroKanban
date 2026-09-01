using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AiProcessing.Infrastructure.Persistence;

public sealed class AiProcessingDbContext : AppDbContextBase
{
    public AiProcessingDbContext(DbContextOptions<AiProcessingDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("ai_processing");
        modelBuilder.ApplyConfiguration(new Configurations.LlmOperationConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.LlmPromptVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.LlmResultConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.LlmReviewConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ChunkReferenceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ReviewPolicyConfiguration());
    }

    public DbSet<AiProcessing.Domain.Aggregates.LlmOperation> LlmOperations => Set<AiProcessing.Domain.Aggregates.LlmOperation>();
    public DbSet<AiProcessing.Domain.Aggregates.LlmPromptVersion> LlmPromptVersions => Set<AiProcessing.Domain.Aggregates.LlmPromptVersion>();
    public DbSet<AiProcessing.Domain.Aggregates.LlmResult> LlmResults => Set<AiProcessing.Domain.Aggregates.LlmResult>();
    public DbSet<AiProcessing.Domain.Aggregates.LlmReview> LlmReviews => Set<AiProcessing.Domain.Aggregates.LlmReview>();
}
