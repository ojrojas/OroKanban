using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

namespace AiProcessing.Infrastructure.Persistence;

public sealed class AiProcessingDbContext : AppDbContextBase
{
    public AiProcessingDbContext(DbContextOptions<AiProcessingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("aiprocessing");
    }
}