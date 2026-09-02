using BuildingBlocks.Kernel.Domain.Entities;

using Notifications.Domain.Ids;
using Notifications.Domain.Enumerations;
using Notifications.Domain.Events;
using Notifications.Domain.Rules;
using Notifications.Domain.ValueObjects;

namespace Notifications.Domain.Aggregates;

public sealed class Notification : AggregateRoot<NotificationId>
{
    public Guid RecipientId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid SourceEventId { get; private set; }
    public Guid? SourceResourceId { get; private set; }
    public string? SourceResourceType { get; private set; }
    public NotificationType NotificationType { get; private set; } = default!;
    public Channel Channel { get; private set; } = default!;
    public DeliveryState DeliveryState { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public string Link { get; private set; } = default!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public Guid? CorrelationId { get; private set; }

    private Notification() { }

    private Notification(NotificationId id, Guid recipientId, Guid? tenantId, Guid sourceEventId, Guid? sourceResourceId, string? sourceResourceType, NotificationType type, Channel channel, DeliveryState deliveryState, NotificationContent content, DateTime createdAt, Guid? correlationId)
    {
        Id = id;
        RecipientId = recipientId;
        TenantId = tenantId;
        SourceEventId = sourceEventId;
        SourceResourceId = sourceResourceId;
        SourceResourceType = sourceResourceType;
        NotificationType = type;
        Channel = channel;
        DeliveryState = deliveryState;
        Title = content.Title;
        Body = content.Body;
        Link = content.Link;
        CreatedAt = createdAt;
        CorrelationId = correlationId;
    }

    public static Notification Create(Guid recipientId, Guid? tenantId, Guid sourceEventId, Guid? sourceResourceId, string? sourceResourceType, NotificationType type, Channel channel, NotificationContent content, Guid? correlationId)
    {
        CheckRule(new DedupeKeyRequiredRule(sourceEventId, recipientId));
        CheckRule(new TitleRequiredRule(content.Title));
        CheckRule(new LinkRequiredRule(content.Link));

        var notification = new Notification(NotificationId.New(), recipientId, tenantId, sourceEventId, sourceResourceId, sourceResourceType, type, channel, DeliveryState.Delivered, content, DateTime.UtcNow, correlationId);
        notification.RaiseDomainEvent(new NotificationCreatedDomainEvent(notification.Id.Value, recipientId, type.Id, channel.Id, sourceEventId, notification.CreatedAt, correlationId));
        return notification;
    }

    public bool MarkRead()
    {
        if (ReadAt != null) return false;
        ReadAt = DateTime.UtcNow;
        RaiseDomainEvent(new NotificationReadDomainEvent(Id.Value, RecipientId, ReadAt.Value));
        return true;
    }
}
