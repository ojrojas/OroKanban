using BuildingBlocks.EventBus.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Notifications.Domain.Aggregates;
using Notifications.Domain.Enumerations;
using Notifications.Domain.Ids;
using Notifications.Domain.Services;
using Notifications.Infrastructure.Channels;
using Notifications.Infrastructure.Services;
using Notifications.Infrastructure.Specifications;

namespace Notifications.Infrastructure.Consumers;

public sealed class NotificationDispatcher(
    IRepository<Notification, NotificationId> notifications,
    IRepository<NotificationPreference, Guid> preferences,
    INotificationPolicy policy,
    INotificationContentPolicy contentPolicy,
    IChannelRouter router,
    ILogger<NotificationDispatcher> logger,
    Notifications.Infrastructure.Persistence.NotificationsDbContext dbContext)
{
    public async Task HandleAsync(IntegrationEvent evt, NotificationType type, Guid? sourceResourceId, string? sourceResourceType, Guid? tenantId, Guid? correlationId, CancellationToken ct)
    {
        var recipients = NotificationPolicy.ResolveRecipients(evt);
        if (recipients.Count == 0)
        {
            logger.LogInformation("No recipients for event {EventId} type {Type}", evt.Id, type.Name);
            return;
        }

        foreach (var recipientId in recipients)
        {
            if (recipientId == Guid.Empty) continue;

            // Load preferences for recipient
            var prefSpec = new PreferenceByUserSpec(recipientId);
            var pref = await preferences.FirstOrDefaultAsync(prefSpec, ct);
            var rawPrefs = pref?.Preferences ?? new Dictionary<int, Dictionary<int, bool>>();
            var userPrefs = rawPrefs.ToDictionary(k => k.Key, v => (IReadOnlyDictionary<int, bool>)v.Value);
            // Check policy for InApp first
            var channel = Channel.InApp;
            if (!policy.IsEnabled(type, channel, userPrefs))
            {
                logger.LogInformation("Skipped notification for {Recipient} type {Type} channel {Channel} due to preference/policy", recipientId, type.Name, channel.Name);
                continue;
            }

            var content = contentPolicy.Compose(type, sourceResourceId ?? evt.Id, sourceResourceType, evt.Id);

            // Determine per-channel notifications — for MVP only InApp, Email as second if enabled and not failing would be second row
            var channelsToDeliver = new List<Channel> { Channel.InApp };
            // Check Email if enabled (policy will decide) — for fan-out demonstration we also handle Email if IsEnabled
            if (policy.IsEnabled(type, Channel.Email, userPrefs))
            {
                channelsToDeliver.Add(Channel.Email);
            }

            foreach (var ch in channelsToDeliver)
            {
                var notification = Notification.Create(recipientId, tenantId, evt.Id, sourceResourceId, sourceResourceType, type, ch, content, correlationId);
                try
                {
                    await notifications.AddAsync(notification, ct);
                    await dbContext.SaveChangesAsync(ct);
                    await router.FanOutAsync(notification, ct);
                }
                catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505")
                {
                    logger.LogInformation("Duplicate notification deduped {SourceEventId} {RecipientId} {Channel}", evt.Id, recipientId, ch.Name);
                    // Swallow — already delivered
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to deliver notification {SourceEventId} {RecipientId} {Channel}", evt.Id, recipientId, ch.Name);
                    throw;
                }
            }
        }
    }
}

// Thin adapters per event type
public sealed class WorkItemAssignedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<Projects.Contracts.Events.WorkItemAssignedIntegrationEvent>
{
    public Task HandleAsync(Projects.Contracts.Events.WorkItemAssignedIntegrationEvent evt, CancellationToken ct)
        => dispatcher.HandleAsync(evt, NotificationType.WorkItemAssigned, evt.WorkItemId, "WorkItem", evt.TenantId, null, ct);
}

public sealed class WorkItemStatusChangedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<Projects.Contracts.Events.WorkItemStatusChangedIntegrationEvent>
{
    public Task HandleAsync(Projects.Contracts.Events.WorkItemStatusChangedIntegrationEvent evt, CancellationToken ct)
    {
        var type = evt.ToId switch
        {
            3 => NotificationType.WorkItemOverdue,
            4 => NotificationType.WorkItemBlocked,
            5 => NotificationType.WorkItemCompleted,
            6 => NotificationType.ReviewRequested,
            _ => NotificationType.WorkItemBlocked
        };
        return dispatcher.HandleAsync(evt, type, evt.WorkItemId, "WorkItem", evt.TenantId, null, ct);
    }
}

public sealed class DocumentUploadedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<Documents.Contracts.Events.DocumentUploadedIntegrationEvent>
{
    public Task HandleAsync(Documents.Contracts.Events.DocumentUploadedIntegrationEvent evt, CancellationToken ct)
        => dispatcher.HandleAsync(evt, NotificationType.DocumentUploaded, evt.DocumentId, "Document", evt.TenantId, null, ct);
}

public sealed class DocumentApprovedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<Documents.Contracts.Events.DocumentApprovedIntegrationEvent>
{
    public Task HandleAsync(Documents.Contracts.Events.DocumentApprovedIntegrationEvent evt, CancellationToken ct)
        => dispatcher.HandleAsync(evt, NotificationType.DocumentApproved, evt.DocumentId, "Document", null, null, ct);
}

public sealed class DocumentClassifiedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<Documents.Contracts.Events.DocumentClassifiedIntegrationEvent>
{
    public Task HandleAsync(Documents.Contracts.Events.DocumentClassifiedIntegrationEvent evt, CancellationToken ct)
        => dispatcher.HandleAsync(evt, NotificationType.DocumentClassified, evt.DocumentId, "Document", null, null, ct);
}

public sealed class LlmResultGeneratedHandler(NotificationDispatcher dispatcher) : IIntegrationEventHandler<AiProcessing.Contracts.Events.LlmResultGeneratedIntegrationEvent>
{
    public Task HandleAsync(AiProcessing.Contracts.Events.LlmResultGeneratedIntegrationEvent evt, CancellationToken ct)
        => dispatcher.HandleAsync(evt, NotificationType.AiReviewRequested, evt.DocumentId, "LlmResult", evt.TenantId, null, ct);
}
