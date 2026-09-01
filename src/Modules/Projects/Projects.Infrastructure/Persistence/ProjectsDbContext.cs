using BuildingBlocks.Kernel.Domain.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Projects.Infrastructure.Persistence;

public sealed class ProjectsDbContext : AppDbContextBase
{
    public ProjectsDbContext(DbContextOptions<ProjectsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("projects");
    }
}
