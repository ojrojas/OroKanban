using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Metrics.Infrastructure.Persistence;

public sealed class MetricsDbContext : AppDbContextBase
{
    public MetricsDbContext(DbContextOptions<MetricsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("metrics");
    }
}
