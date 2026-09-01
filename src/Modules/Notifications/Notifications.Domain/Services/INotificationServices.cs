using Notifications.Domain.Enumerations;
using Notifications.Domain.ValueObjects;

namespace Notifications.Domain.Services;

public interface INotificationPolicy
{
    IReadOnlySet<(int TypeId, int ChannelId)> MandatedTypes { get; }
    IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> DefaultPreferences { get; }
    bool IsEnabled(NotificationType type, Channel channel, IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> userPrefs);
}

public interface INotificationContentPolicy
{
    NotificationContent Compose(NotificationType type, Guid sourceResourceId, string? sourceResourceType, Guid sourceEventId);
}
