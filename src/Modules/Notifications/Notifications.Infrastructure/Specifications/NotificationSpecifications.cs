using BuildingBlocks.Kernel.Domain.Specifications;
using Notifications.Domain.Aggregates;

namespace Notifications.Infrastructure.Specifications;

public sealed class NotificationByRecipientSpec : Specification<Notification>
{
    public NotificationByRecipientSpec(Guid recipientId, Guid? tenantId = null, bool unreadOnly = false)
    {
        Where(n => n.RecipientId == recipientId);
        if (tenantId.HasValue) Where(n => n.TenantId == tenantId.Value);
        if (unreadOnly) Where(n => n.ReadAt == null);
    }
}

public sealed class UnreadNotificationsSpec : Specification<Notification>
{
    public UnreadNotificationsSpec(Guid recipientId, Guid? tenantId = null)
    {
        Where(n => n.RecipientId == recipientId && n.ReadAt == null);
        if (tenantId.HasValue) Where(n => n.TenantId == tenantId.Value);
    }
}

public sealed class NotificationByIdSpec : Specification<Notification>
{
    public NotificationByIdSpec(Guid id)
    {
        Where(n => n.Id.Value == id);
    }
}

public sealed class PreferenceByUserSpec : Specification<NotificationPreference>
{
    public PreferenceByUserSpec(Guid userId)
    {
        Where(p => p.Id == userId);
    }
}
