using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiProcessing.Infrastructure.Persistence;

public sealed class AiProcessingDbContextFactory : IDesignTimeDbContextFactory<AiProcessingDbContext>
{
    public AiProcessingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AiProcessingDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new AiProcessingDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}
