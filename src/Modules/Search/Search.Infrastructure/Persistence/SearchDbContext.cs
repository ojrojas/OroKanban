using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Search.Infrastructure.Persistence;

public sealed class SearchDbContext : AppDbContextBase
{
    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("search");
    }
}