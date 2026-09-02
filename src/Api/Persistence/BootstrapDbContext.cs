using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Notifications.Domain.Aggregates;
using Projects.Domain.Aggregates;
using Metrics.Domain.Aggregates;
using Documents.Domain.Aggregates;
using Documents.Domain.Entities;
using Audit.Domain.Aggregates;
using Organization.Domain.Aggregates;
using AiProcessing.Domain.Aggregates;

namespace Api.Persistence;

/// <summary>
/// Single DbContext that includes ALL entities from ALL modules.
/// Used only at startup with EnsureCreated to create all schemas/tables in one pass.
/// Each module still uses its own DbContext at runtime.
/// </summary>
public sealed class BootstrapDbContext : AppDbContextBase
{
    public BootstrapDbContext(DbContextOptions<BootstrapDbContext> options) : base(options) { }

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();

    // Metrics
    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();
    public DbSet<Metrics.Domain.Aggregates.Milestone> MetricMilestones => Set<Metrics.Domain.Aggregates.Milestone>();
    public DbSet<ProgressExplanation> ProgressExplanations => Set<ProgressExplanation>();

    // Documents
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<DocumentProcessingJob> ProcessingJobs => Set<DocumentProcessingJob>();
    public DbSet<DocumentAccessEntry> AccessEntries => Set<DocumentAccessEntry>();
    public DbSet<DocumentExplicitGrant> ExplicitGrants => Set<DocumentExplicitGrant>();

    // Audit
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    // Organization
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<ManagementRelationship> ManagementRelationships => Set<ManagementRelationship>();
    public DbSet<ExplicitGrant> OrgExplicitGrants => Set<ExplicitGrant>();

    // AiProcessing
    public DbSet<LlmOperation> LlmOperations => Set<LlmOperation>();
    public DbSet<LlmPromptVersion> LlmPromptVersions => Set<LlmPromptVersion>();
    public DbSet<LlmResult> LlmResults => Set<LlmResult>();
    public DbSet<LlmReview> LlmReviews => Set<LlmReview>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Notifications.Infrastructure.Persistence.NotificationsDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Projects.Infrastructure.Persistence.ProjectsDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Metrics.Infrastructure.Persistence.MetricsDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Documents.Infrastructure.Persistence.DocumentsDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Audit.Infrastructure.Persistence.AuditDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(Organization.Infrastructure.Persistence.OrganizationDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiProcessing.Infrastructure.Persistence.AiProcessingDbContext).Assembly);
    }
}
