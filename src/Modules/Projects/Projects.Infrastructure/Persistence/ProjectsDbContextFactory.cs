using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Projects.Infrastructure.Persistence;

public sealed class ProjectsDbContextFactory : IDesignTimeDbContextFactory<ProjectsDbContext>
{
    public ProjectsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new ProjectsDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__orokanban_projects")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}
