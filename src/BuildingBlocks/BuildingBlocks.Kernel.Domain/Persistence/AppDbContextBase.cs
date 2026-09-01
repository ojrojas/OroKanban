using BuildingBlocks.Kernel.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Kernel.Domain.Persistence;

public abstract class AppDbContextBase : DbContext
{
    protected AppDbContextBase(DbContextOptions options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());

        // Apply RowVersion concurrency token to any entity with a byte[] RowVersion property
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersionProp = entityType.FindProperty("RowVersion");
            if (rowVersionProp is not null && rowVersionProp.ClrType == typeof(byte[]))
            {
                rowVersionProp.IsConcurrencyToken = true;
                rowVersionProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
            }
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Collect domain events from tracked aggregates before save
        var aggregates = ChangeTracker.Entries<IAggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        // Clear after save (dispatch would happen here via IDomainEventDispatcher in full implementation)
        foreach (var agg in aggregates)
        {
            agg.ClearDomainEvents();
        }

        return result;
    }
}
