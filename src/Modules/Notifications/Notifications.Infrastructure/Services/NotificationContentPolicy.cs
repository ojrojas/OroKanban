using Notifications.Domain.Enumerations;
using Notifications.Domain.Services;
using Notifications.Domain.ValueObjects;

namespace Notifications.Infrastructure.Services;

public sealed class NotificationContentPolicy : INotificationContentPolicy
{
    public NotificationContent Compose(NotificationType type, Guid sourceResourceId, string? sourceResourceType, Guid sourceEventId)
    {
        // Allowlist per type — metadata + link only, never body/payload
        var title = type.Id switch
        {
            1 => $"You were assigned work item {sourceResourceId}",
            2 => $"Work item {sourceResourceId} reassigned",
            3 => $"Work item {sourceResourceId} is overdue",
            4 => $"Work item {sourceResourceId} blocked",
            5 => $"Work item {sourceResourceId} completed",
            6 => $"Review requested for {sourceResourceId}",
            7 => $"Document {sourceResourceId} uploaded",
            8 => $"Document {sourceResourceId} classified",
            9 => $"Document {sourceResourceId} approved",
            10 => $"AI review requested for {sourceResourceId}",
            11 => $"Risk increased for project {sourceResourceId}",
            _ => $"Notification {type.Name} for {sourceResourceId}"
        };
        var body = type.Id switch
        {
            9 => $"Document {sourceResourceId} was approved — open to view",
            10 => $"AI review {sourceEventId} for document {sourceResourceId} — open to review",
            _ => $"Event {sourceEventId} for {sourceResourceType ?? "resource"} {sourceResourceId}"
        };
        var link = type.Id switch
        {
            1 or 2 or 3 or 4 or 5 or 6 => $"/projects/unknown/work-items/{sourceResourceId}",
            7 or 8 or 9 => $"/documents/{sourceResourceId}",
            10 => $"/documents/{sourceResourceId}/ai-results/{sourceEventId}",
            11 => $"/projects/{sourceResourceId}/risks",
            _ => $"/notifications/{sourceEventId}"
        };
        // Ensure no forbidden payload markers
        return new NotificationContent(title, body, link);
    }
}
