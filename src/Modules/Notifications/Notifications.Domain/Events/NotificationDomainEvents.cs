using BuildingBlocks.Kernel.Domain.Events;

namespace Notifications.Domain.Events;

public sealed record NotificationCreatedDomainEvent(
    Guid NotificationId,
    Guid RecipientId,
    int NotificationTypeId,
    int ChannelId,
    Guid SourceEventId,
    DateTime CreatedAt,
    Guid? CorrelationId) : DomainEvent;

public sealed record NotificationReadDomainEvent(
    Guid NotificationId,
    Guid RecipientId,
    DateTime ReadAt) : DomainEvent;

public sealed record PreferencesUpdatedDomainEvent(
    Guid UserId,
    Guid TenantId,
    DateTime UpdatedAt) : DomainEvent;
