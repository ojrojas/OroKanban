using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContext : AppDbContextBase
{
    public DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.ApplyConfiguration(new Configurations.DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DocumentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DocumentProcessingJobConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DocumentAccessEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DocumentExplicitGrantConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ClassificationRuleConfiguration());
    }

    public DbSet<Domain.Aggregates.Document> Documents => Set<Domain.Aggregates.Document>();
    public DbSet<Domain.Aggregates.DocumentVersion> DocumentVersions => Set<Domain.Aggregates.DocumentVersion>();
    public DbSet<Domain.Aggregates.DocumentProcessingJob> ProcessingJobs => Set<Domain.Aggregates.DocumentProcessingJob>();
    public DbSet<Domain.Entities.DocumentAccessEntry> AccessEntries => Set<Domain.Entities.DocumentAccessEntry>();
    public DbSet<Domain.Entities.DocumentExplicitGrant> ExplicitGrants => Set<Domain.Entities.DocumentExplicitGrant>();
    public DbSet<Configurations.ClassificationRule> ClassificationRules => Set<Configurations.ClassificationRule>();
}