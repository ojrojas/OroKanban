using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;

namespace Organization.Infrastructure.Persistence.Configurations;

public sealed class OrganizationUnitConfiguration : IEntityTypeConfiguration<OrganizationUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationUnit> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new OrganizationUnitId(v));
        builder.Property(x => x.ParentId).HasConversion(
            id => id != null ? id.Value : (Guid?)null,
            v => v != null ? new OrganizationUnitId(v.Value) : null);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // HierarchyPath — persist as single string column via ValueConverter (joined by "/")
        var converter = new ValueConverter<HierarchyPath, string>(
            v => string.Join("/", v.Segments),
            v => new HierarchyPath(v.Split('/', StringSplitOptions.RemoveEmptyEntries)));

        builder.Property(x => x.HierarchyPath)
            .HasConversion(converter)
            .HasColumnName("hierarchy_path")
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.ParentId });
        builder.ToTable("organization_units", "organization");
    }
}
