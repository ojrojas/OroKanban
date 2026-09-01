using BuildingBlocks.Kernel.Domain.Persistence;

using Microsoft.EntityFrameworkCore;
using Notifications.Infrastructure.Persistence.Configurations;

namespace Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext : AppDbContextBase
{
    public DbSet<Domain.Aggregates.Notification> Notifications => Set<Domain.Aggregates.Notification>();
    public DbSet<Domain.Aggregates.NotificationPreference> NotificationPreferences => Set<Domain.Aggregates.NotificationPreference>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());
    }
}