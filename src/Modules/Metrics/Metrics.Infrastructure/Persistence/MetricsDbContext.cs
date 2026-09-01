using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Metrics.Domain.Aggregates;

namespace Metrics.Infrastructure.Persistence;

public sealed class MetricsDbContext : AppDbContextBase
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options) : base(options) { }

    public DbSet<MetricDefinition> MetricDefinitions => Set<MetricDefinition>();
    public DbSet<MetricValue> MetricValues => Set<MetricValue>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<ProgressExplanation> ProgressExplanations => Set<ProgressExplanation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("metrics");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MetricsDbContext).Assembly);
    }
}
