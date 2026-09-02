using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Persistence;

public sealed class AuditDbContext : AppDbContextBase
{
    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfiguration(new Configurations.AuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.AuditConsumedEventConfiguration());
    }

    public DbSet<Domain.Aggregates.AuditEntry> AuditEntries => Set<Domain.Aggregates.AuditEntry>();
    public DbSet<Configurations.AuditConsumedEvent> AuditConsumedEvents => Set<Configurations.AuditConsumedEvent>();
}