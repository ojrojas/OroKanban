using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

using Projects.Domain.Aggregates;

namespace Projects.Infrastructure.Persistence;

public sealed class ProjectsDbContext : AppDbContextBase
{
    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<WorkItemDeliverable> WorkItemDeliverables => Set<WorkItemDeliverable>();
    public DbSet<WorkItemHistory> WorkItemHistories => Set<WorkItemHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("projects");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProjectsDbContext).Assembly);
    }
}