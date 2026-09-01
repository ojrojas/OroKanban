using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Audit.Infrastructure.Persistence.Configurations;

public sealed class AuditConsumedEvent
{
    public Guid EventId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public string Action { get; set; } = default!;
    public Guid CorrelationId { get; set; }
}

public sealed class AuditConsumedEventConfiguration : IEntityTypeConfiguration<AuditConsumedEvent>
{
    public void Configure(EntityTypeBuilder<AuditConsumedEvent> b)
    {
        b.ToTable("audit_consumed_events", "audit");
        b.HasKey(x => x.EventId);
        b.Property(x => x.EventId).IsRequired();
        b.HasIndex(x => x.EventId).IsUnique();
    }
}
