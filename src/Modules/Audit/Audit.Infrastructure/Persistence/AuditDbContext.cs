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
    }
}
