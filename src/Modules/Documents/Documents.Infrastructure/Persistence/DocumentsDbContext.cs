using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Documents.Infrastructure.Persistence;

public sealed class DocumentsDbContext : AppDbContextBase
{
    public DocumentsDbContext(DbContextOptions<DocumentsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("documents");
    }
}