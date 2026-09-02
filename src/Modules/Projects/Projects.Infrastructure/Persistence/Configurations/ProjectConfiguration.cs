using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Projects.Domain.Aggregates;

namespace Projects.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects", "projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.Ids.ProjectId(v));
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.OwnsMany(x => x.Members, mb =>
        {
            mb.ToTable("project_members", "projects");
            mb.WithOwner().HasForeignKey("ProjectId");
            mb.Property<Guid>("Id");
            mb.HasKey("Id");
            mb.Property(x => x.UserId).IsRequired();
            mb.Property(x => x.RoleId).IsRequired();
            mb.Property(x => x.JoinedAt).IsRequired();
            mb.HasIndex("ProjectId", "UserId").IsUnique();
        });
        b.OwnsMany(x => x.Milestones, ob =>
        {
            ob.ToTable("project_milestones", "projects");
            ob.WithOwner().HasForeignKey("ProjectId");
            ob.Property<Guid>("Id");
            ob.HasKey("Id");
            ob.Property(x => x.Title).IsRequired().HasMaxLength(200);
        });
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> b)
    {
        b.ToTable("work_items", "projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.Ids.WorkItemId(v));
        b.Property(x => x.Title).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(10000);
        b.Property(x => x.TagsJson).HasColumnName("tags_json").HasColumnType("jsonb");
        b.HasIndex(x => new { x.ProjectId, x.ParentId });
        b.HasIndex(x => new { x.TenantId, x.ProjectId });
        b.HasIndex(x => x.ResponsibleId);
        b.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        b.Property(x => x.Version).IsRequired().IsConcurrencyToken(false);
        b.Ignore(x => x.Tags);
        b.Ignore(x => x.DomainEvents);
    }
}

public sealed class WorkItemDependencyConfiguration : IEntityTypeConfiguration<WorkItemDependency>
{
    public void Configure(EntityTypeBuilder<WorkItemDependency> b)
    {
        b.ToTable("work_item_dependencies", "projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.Ids.WorkItemDependencyId(v));
        b.HasIndex(x => new { x.DependentId, x.PrincipalId }).IsUnique();
        b.HasIndex(x => x.DependentId);
        b.HasIndex(x => x.PrincipalId);
        b.Ignore(x => x.DomainEvents);
    }
}