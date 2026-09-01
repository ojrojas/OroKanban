using BuildingBlocks.EventBus.Abstractions;
using Microsoft.Extensions.Options;
using Notifications.Domain.Enumerations;
using Notifications.Domain.Services;
using Notifications.Infrastructure.Configuration;

namespace Notifications.Infrastructure.Services;

public sealed class NotificationPolicy : INotificationPolicy
{
    private readonly NotificationsOptions _options;
    public NotificationPolicy(IOptions<NotificationsOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlySet<(int TypeId, int ChannelId)> MandatedTypes => _options.MandatedTypes;

    public IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> DefaultPreferences => new Dictionary<int, IReadOnlyDictionary<int, bool>>();

    public bool IsEnabled(NotificationType type, Channel channel, IReadOnlyDictionary<int, IReadOnlyDictionary<int, bool>> userPrefs)
    {
        if (MandatedTypes.Contains((type.Id, channel.Id))) return true;
        if (userPrefs.TryGetValue(type.Id, out var channelMap) && channelMap.TryGetValue(channel.Id, out var enabled)) return enabled;
        // default InApp true, Email false
        return channel.Id == Channel.InApp.Id;
    }

    public static IReadOnlyList<Guid> ResolveRecipients(IntegrationEvent evt)
    {
        // Resolve based on event type via pattern matching
        return evt switch
        {
            Projects.Contracts.Events.WorkItemAssignedIntegrationEvent e => [e.AssigneeId],
            Projects.Contracts.Events.WorkItemStatusChangedIntegrationEvent e => [e.ActorId], // simplified: notify actor; full would need owner/assignee
            Documents.Contracts.Events.DocumentUploadedIntegrationEvent e => [e.OwnerId],
            Documents.Contracts.Events.DocumentApprovedIntegrationEvent e => [e.ApproverId],
            Documents.Contracts.Events.DocumentClassifiedIntegrationEvent e => [e.ActorId],
            AiProcessing.Contracts.Events.LlmResultGeneratedIntegrationEvent e => [Guid.Empty], // handled via caller? for now skip
            _ => []
        };
    }
}
