using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain.Aggregates;
using Notifications.Domain.Ids;

namespace Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, v => new NotificationId(v)).ValueGeneratedNever();
        builder.Property(x => x.RecipientId).IsRequired();
        builder.Property(x => x.TenantId);
        builder.Property(x => x.SourceEventId).IsRequired();
        builder.Property(x => x.SourceResourceId);
        builder.Property(x => x.SourceResourceType).HasMaxLength(100);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Link).IsRequired().HasMaxLength(500);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ReadAt);
        builder.Property(x => x.CorrelationId);

        builder.Property(x => x.NotificationType).HasConversion(
            v => v.Id,
            v => Notifications.Domain.Enumerations.NotificationType.FromId(v))
            .HasColumnName("NotificationTypeId");

        builder.Property(x => x.Channel).HasConversion(
            v => v.Id,
            v => Notifications.Domain.Enumerations.Channel.FromId(v))
            .HasColumnName("ChannelId");

        builder.Property(x => x.DeliveryState).HasConversion(
            v => v.Id,
            v => Notifications.Domain.Enumerations.DeliveryState.FromId(v))
            .HasColumnName("DeliveryStateId");

        builder.HasIndex(x => new { x.SourceEventId, x.RecipientId, x.Channel })
            .IsUnique()
            .HasDatabaseName("IX_notifications_dedupe");
        builder.HasIndex(x => new { x.RecipientId, x.CreatedAt }).HasDatabaseName("IX_notifications_recipient_created");
        builder.HasIndex(x => x.CorrelationId).HasDatabaseName("IX_notifications_correlation");
        builder.HasIndex(x => x.TenantId).HasDatabaseName("IX_notifications_tenant");

        builder.Ignore(x => x.DomainEvents);
    }
}

public sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Preferences).HasColumnName("PreferencesJson").HasColumnType("jsonb")
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<int, Dictionary<int, bool>>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new());
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(x => x.TenantId);
        builder.Ignore(x => x.DomainEvents);
    }
}
