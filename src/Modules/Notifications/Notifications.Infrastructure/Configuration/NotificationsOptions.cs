namespace Notifications.Infrastructure.Configuration;

public sealed class NotificationsOptions
{
    public const string SectionName = "Notifications";
    public HashSet<(int TypeId, int ChannelId)> MandatedTypes { get; set; } = new()
    {
        (3, 1), // WorkItemOverdue InApp
        (4, 1), // WorkItemBlocked InApp
        (11, 1) // RiskIncreased InApp
    };
}
