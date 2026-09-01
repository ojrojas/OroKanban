using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    public DocumentsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DocumentsDbContext>()
            .UseNpgsql(GetConnectionString())
            .Options;

        return new DocumentsDbContext(options);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable("ConnectionStrings__orokanban")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__orokanban_documents")
            ?? "Host=localhost;Port=5432;Database=orokanban;Username=postgres;Password=postgres";
    }
}
