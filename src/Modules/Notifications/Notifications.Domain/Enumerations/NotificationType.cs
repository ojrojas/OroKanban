using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Notifications.Domain.Enumerations;

public sealed class NotificationType : Enumeration<NotificationType>
{
    public static readonly NotificationType WorkItemAssigned = new(1, nameof(WorkItemAssigned));
    public static readonly NotificationType WorkItemReassigned = new(2, nameof(WorkItemReassigned));
    public static readonly NotificationType WorkItemOverdue = new(3, nameof(WorkItemOverdue));
    public static readonly NotificationType WorkItemBlocked = new(4, nameof(WorkItemBlocked));
    public static readonly NotificationType WorkItemCompleted = new(5, nameof(WorkItemCompleted));
    public static readonly NotificationType ReviewRequested = new(6, nameof(ReviewRequested));
    public static readonly NotificationType DocumentUploaded = new(7, nameof(DocumentUploaded));
    public static readonly NotificationType DocumentClassified = new(8, nameof(DocumentClassified));
    public static readonly NotificationType DocumentApproved = new(9, nameof(DocumentApproved));
    public static readonly NotificationType AiReviewRequested = new(10, nameof(AiReviewRequested));
    public static readonly NotificationType RiskIncreased = new(11, nameof(RiskIncreased));

    private NotificationType(int id, string name) : base(id, name) { }
}
