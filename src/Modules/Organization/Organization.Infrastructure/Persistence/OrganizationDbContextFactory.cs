using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Organization.Infrastructure.Persistence;

public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OrganizationDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new OrganizationDbContext(options);
    }

    private static string GetConnectionString()
    {
        // Design-time fallback: Aspire's postgres resource name "orokanban" is not available outside the host.
        // Use a local default that works with `podman`/`docker` postgres or with `dotnet ef` without a running DB.
        // At runtime, Api's AddNpgsqlDbContext("orokanban") (Aspire) will override this via configuration.
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__orokanban_identity")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}