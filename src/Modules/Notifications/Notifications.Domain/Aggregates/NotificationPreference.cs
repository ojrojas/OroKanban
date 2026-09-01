using BuildingBlocks.Kernel.Domain.Entities;
using Notifications.Domain.Events;

namespace Notifications.Domain.Aggregates;

public sealed class NotificationPreference : AggregateRoot<Guid>
{
    public Guid TenantId { get; private set; }
    public Dictionary<int, Dictionary<int, bool>> Preferences { get; private set; } = new();
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private NotificationPreference() { }

    public static NotificationPreference Create(Guid userId, Guid tenantId, Dictionary<int, Dictionary<int, bool>> preferences)
    {
        var pref = new NotificationPreference
        {
            Id = userId,
            TenantId = tenantId,
            Preferences = preferences ?? new(),
            UpdatedAt = DateTime.UtcNow
        };
        pref.RaiseDomainEvent(new PreferencesUpdatedDomainEvent(userId, tenantId, pref.UpdatedAt));
        return pref;
    }

    public void Update(Dictionary<int, Dictionary<int, bool>> newPreferences)
    {
        Preferences = newPreferences ?? new();
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new PreferencesUpdatedDomainEvent(Id, TenantId, UpdatedAt));
    }
}
