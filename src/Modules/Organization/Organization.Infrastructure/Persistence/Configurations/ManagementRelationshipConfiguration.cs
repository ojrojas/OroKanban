using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Organization.Domain.Aggregates;

namespace Organization.Infrastructure.Persistence.Configurations;

public sealed class ManagementRelationshipConfiguration : IEntityTypeConfiguration<ManagementRelationship>
{
    public void Configure(EntityTypeBuilder<ManagementRelationship> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.ValueObjects.ManagementRelationshipId(v));
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.ManagerId).IsRequired();
        builder.Property(x => x.SubordinateId).IsRequired();
        builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
        builder.Property(x => x.OrganizationUnitId).HasConversion(id => id != null ? id.Value : (Guid?)null, v => v != null ? new Domain.ValueObjects.OrganizationUnitId(v.Value) : null);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.ManagerId });
        builder.HasIndex(x => new { x.TenantId, x.SubordinateId });
        // Filtered unique index for single active per subordinate/unit — handled via migration SQL
        builder.ToTable("management_relationships", "organization");
    }
}