using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Organization.Domain.Aggregates;

namespace Organization.Infrastructure.Persistence.Configurations;

public sealed class ExplicitGrantConfiguration : IEntityTypeConfiguration<ExplicitGrant>
{
    public void Configure(EntityTypeBuilder<ExplicitGrant> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new Organization.Domain.ValueObjects.ExplicitGrantId(v));
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.GranteeUserId).IsRequired();
        builder.Property(x => x.GrantedBy).IsRequired();
        builder.Property(x => x.ResourceType).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ResourceId).IsRequired();
        builder.Property(x => x.Permission).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TenantId, x.GranteeUserId });
        builder.HasIndex(x => new { x.TenantId, x.ResourceType, x.ResourceId });
        builder.ToTable("explicit_grants", "organization");
    }
}
