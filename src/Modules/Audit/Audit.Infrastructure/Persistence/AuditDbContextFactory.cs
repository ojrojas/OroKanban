using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Audit.Infrastructure.Persistence;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AuditDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}
