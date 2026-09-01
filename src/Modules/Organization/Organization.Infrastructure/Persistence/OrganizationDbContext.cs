using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext : AppDbContextBase
{
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("organization");
    }
}
