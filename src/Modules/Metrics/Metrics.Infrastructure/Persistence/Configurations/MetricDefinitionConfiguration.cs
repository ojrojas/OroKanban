using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Metrics.Domain.Aggregates;
using Metrics.Domain.Ids;

namespace Metrics.Infrastructure.Persistence.Configurations;

public sealed class MetricDefinitionConfiguration : IEntityTypeConfiguration<MetricDefinition>
{
    public void Configure(EntityTypeBuilder<MetricDefinition> b)
    {
        b.ToTable("metric_definitions", "metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new MetricDefinitionId(v));
        b.Property(x => x.Code).IsRequired().HasMaxLength(100);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.ProjectId, x.Code }).IsUnique();
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class MetricValueConfiguration : IEntityTypeConfiguration<MetricValue>
{
    public void Configure(EntityTypeBuilder<MetricValue> b)
    {
        b.ToTable("metric_values", "metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new MetricValueId(v));
        b.Property(x => x.DefinitionId).HasConversion(id => id.Value, v => new MetricDefinitionId(v));
        b.HasIndex(x => new { x.ProjectId, x.DefinitionId });
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class MilestoneConfiguration : IEntityTypeConfiguration<Milestone>
{
    public void Configure(EntityTypeBuilder<Milestone> b)
    {
        b.ToTable("metric_milestones", "metrics");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new MilestoneId(v));
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class ProgressExplanationConfiguration : IEntityTypeConfiguration<ProgressExplanation>
{
    public void Configure(EntityTypeBuilder<ProgressExplanation> b)
    {
        b.ToTable("progress_explanations", "metrics");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.ProjectId, x.WorkItemId });
        b.Ignore(x => x.DomainEvents);
    }
}
