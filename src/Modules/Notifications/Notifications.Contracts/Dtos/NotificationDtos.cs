namespace Notifications.Contracts.Dtos;

public sealed record NotificationResponse(
    Guid NotificationId,
    Guid RecipientId,
    string Type,
    int TypeId,
    string Channel,
    int ChannelId,
    string Title,
    string Body,
    string Link,
    string DeliveryState,
    int DeliveryStateId,
    DateTime CreatedAt,
    DateTime? ReadAt,
    Guid SourceEventId,
    Guid? SourceResourceId,
    string? SourceResourceType,
    Guid? CorrelationId);

public sealed record PagedNotificationsResponse(
    IReadOnlyList<NotificationResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    string? Link);

public sealed record UnreadCountResponse(Guid RecipientId, int UnreadCount);
