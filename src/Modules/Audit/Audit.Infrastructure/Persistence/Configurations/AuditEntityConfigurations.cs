using Audit.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> b)
    {
        b.ToTable("audit_entries", "audit");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasConversion(id => id.Value, v => new Domain.Ids.AuditEntryId(v));
        b.HasIndex(x => new { x.TenantId, x.Timestamp });
        b.HasIndex(x => new { x.ResourceType, x.ResourceId });
        b.HasIndex(x => x.CorrelationId);
        b.HasIndex(x => x.OrganizationId);
        b.HasIndex(x => x.ProjectId);
        b.Property(x => x.Action).HasConversion(a => a.Id, id => Domain.Enumerations.AuditAction.FromId(id));
        b.Property(x => x.Timestamp).IsRequired();
        b.Property(x => x.ResourceType).IsRequired().HasMaxLength(100);
        b.Property(x => x.ResourceId).IsRequired().HasMaxLength(200);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.OwnsOne(x => x.Actor, a => {
            a.Property(p => p.ActorId).HasColumnName("ActorId");
            a.Property(p => p.ActorType).HasColumnName("ActorType");
            a.Property(p => p.DisplayName).HasColumnName("ActorDisplayName");
        });
        b.OwnsOne(x => x.Result, r => {
            r.Property(p => p.Result).HasColumnName("Result");
            r.Property(p => p.ErrorCode).HasColumnName("ErrorCode");
        });
        b.OwnsOne(x => x.Snapshot, s => {
            s.Property(p => p.BeforeJson).HasColumnName("BeforeJson").HasColumnType("jsonb");
            s.Property(p => p.AfterJson).HasColumnName("AfterJson").HasColumnType("jsonb");
        });
    }
}
