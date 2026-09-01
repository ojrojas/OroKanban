using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Infrastructure.Persistence.Configurations;

namespace Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContext : AppDbContextBase
{
    public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
    {
    }

    public DbSet<ManagementRelationship> ManagementRelationships => Set<ManagementRelationship>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<ExplicitGrant> ExplicitGrants => Set<ExplicitGrant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("organization");
        modelBuilder.ApplyConfiguration(new ManagementRelationshipConfiguration());
        modelBuilder.ApplyConfiguration(new OrganizationUnitConfiguration());
        modelBuilder.ApplyConfiguration(new ExplicitGrantConfiguration());
    }
}
