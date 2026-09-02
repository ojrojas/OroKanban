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

    public DbSet<Domain.Aggregates.LlmOperation> LlmOperations => Set<Domain.Aggregates.LlmOperation>();
    public DbSet<Domain.Aggregates.LlmPromptVersion> LlmPromptVersions => Set<Domain.Aggregates.LlmPromptVersion>();
    public DbSet<Domain.Aggregates.LlmResult> LlmResults => Set<Domain.Aggregates.LlmResult>();
    public DbSet<Domain.Aggregates.LlmReview> LlmReviews => Set<Domain.Aggregates.LlmReview>();
}
