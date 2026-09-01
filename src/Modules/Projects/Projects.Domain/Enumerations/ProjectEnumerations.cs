using BuildingBlocks.Kernel.Domain.Enumerations;

namespace Projects.Domain.Enumerations;

public sealed class WorkItemType(int id, string name) : Enumeration<WorkItemType>(id, name)
{
    public static readonly WorkItemType Epic = new(1, "Epic");
    public static readonly WorkItemType Feature = new(2, "Feature");
    public static readonly WorkItemType Task = new(3, "Task");
    public static readonly WorkItemType Subtask = new(4, "Subtask");
}

public sealed class WorkItemStatus(int id, string name) : Enumeration<WorkItemStatus>(id, name)
{
    public static readonly WorkItemStatus Backlog = new(1, "Backlog");
    public static readonly WorkItemStatus Planned = new(2, "Planned");
    public static readonly WorkItemStatus InProgress = new(3, "InProgress");
    public static readonly WorkItemStatus Blocked = new(4, "Blocked");
    public static readonly WorkItemStatus InReview = new(5, "InReview");
    public static readonly WorkItemStatus Completed = new(6, "Completed");
}

public sealed class WorkItemPriority(int id, string name) : Enumeration<WorkItemPriority>(id, name)
{
    public static readonly WorkItemPriority Low = new(1, "Low");
    public static readonly WorkItemPriority Medium = new(2, "Medium");
    public static readonly WorkItemPriority High = new(3, "High");
    public static readonly WorkItemPriority Critical = new(4, "Critical");
    public static readonly WorkItemPriority Urgent = new(5, "Urgent");
}

public sealed class Criticality(int id, string name) : Enumeration<Criticality>(id, name)
{
    public static readonly Criticality Low = new(1, "Low");
    public static readonly Criticality Medium = new(2, "Medium");
    public static readonly Criticality High = new(3, "High");
    public static readonly Criticality Critical = new(4, "Critical");
}

public sealed class DependencyType(int id, string name) : Enumeration<DependencyType>(id, name)
{
    public static readonly DependencyType Blocks = new(1, "Blocks");
    public static readonly DependencyType BlockedBy = new(2, "BlockedBy");
    public static readonly DependencyType DependsOn = new(3, "DependsOn");
    public static readonly DependencyType RelatedTo = new(4, "RelatedTo");
}

public sealed class ProjectStatus(int id, string name) : Enumeration<ProjectStatus>(id, name)
{
    public static readonly ProjectStatus Draft = new(1, "Draft");
    public static readonly ProjectStatus Active = new(2, "Active");
    public static readonly ProjectStatus OnHold = new(3, "OnHold");
    public static readonly ProjectStatus Completed = new(4, "Completed");
    public static readonly ProjectStatus Archived = new(5, "Archived");
}

public sealed class ProjectPriority(int id, string name) : Enumeration<ProjectPriority>(id, name)
{
    public static readonly ProjectPriority Low = new(1, "Low");
    public static readonly ProjectPriority Medium = new(2, "Medium");
    public static readonly ProjectPriority High = new(3, "High");
    public static readonly ProjectPriority Critical = new(4, "Critical");
}

public sealed class ProjectRole(int id, string name) : Enumeration<ProjectRole>(id, name)
{
    public static readonly ProjectRole Owner = new(1, "Owner");
    public static readonly ProjectRole Manager = new(2, "Manager");
    public static readonly ProjectRole Contributor = new(3, "Contributor");
    public static readonly ProjectRole Reviewer = new(4, "Reviewer");
}