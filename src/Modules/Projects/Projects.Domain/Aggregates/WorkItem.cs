using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Rules;

using Projects.Domain.Events;
using Projects.Domain.Ids;
using Projects.Domain.Rules;
using Projects.Domain.Services;
using Projects.Domain.ValueObjects;

namespace Projects.Domain.Aggregates;

public sealed class WorkItem : AggregateRoot<WorkItemId>
{
    public Guid TenantId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Description { get; private set; }
    public int TypeId { get; private set; }
    public int StatusId { get; private set; }
    public int PriorityId { get; private set; }
    public int CriticalityId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public Guid? ResponsibleId { get; private set; }
    public Guid? ReviewerId { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public decimal EstimatedHours { get; private set; }
    public decimal ActualHours { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? TagsJson { get; private set; } // stored as json or via owned? keep string for now, owned via EF owned?
    public IReadOnlyList<string> Tags => string.IsNullOrEmpty(TagsJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson)!;

    public int Version { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WorkItem() { }

    private WorkItem(WorkItemId id, Guid tenantId, Guid projectId, Guid? parentId, string title, string? description, int typeId, int priorityId, int criticalityId, Guid? ownerId, Guid? responsibleId, Guid? reviewerId, DateTime? dueDate, decimal estimatedHours, IReadOnlyList<string> tags, int progress)
        : base(id)
    {
        TenantId = tenantId;
        ProjectId = projectId;
        ParentId = parentId;
        Title = title;
        Description = description;
        TypeId = typeId;
        StatusId = Enumerations.WorkItemStatus.Backlog.Id;
        PriorityId = priorityId;
        CriticalityId = criticalityId;
        OwnerId = ownerId;
        ResponsibleId = responsibleId;
        ReviewerId = reviewerId;
        DueDate = dueDate?.ToUniversalTime();
        EstimatedHours = estimatedHours;
        ActualHours = 0;
        ProgressPercent = progress;
        TagsJson = tags.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(tags);
        Version = 1;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        RaiseDomainEvent(new WorkItemCreatedDomainEvent(id.Value, projectId, tenantId, typeId));
    }

    public static WorkItem Create(Guid tenantId, Guid projectId, Guid? parentId, string title, string? description, int typeId, int priorityId, int criticalityId, Guid? ownerId, Guid? responsibleId, Guid? reviewerId, DateTime? dueDate, decimal estimatedHours, IReadOnlyList<string> tags, int progress)
    {
        CheckRule(new TitleRequiredRule(title));
        // enumeration existence will be validated by handler loading enumeration; here just check type !=0
        if (typeId == 0) throw new BusinessRuleValidationException(new TypeRequiredRule());
        // validate VO
        _ = Effort.FromHours(estimatedHours);
        _ = ProgressValue.FromPercent(progress);
        // tags already validated by VO Tag.Create before passing normalized strings, deduped
        if (parentId.HasValue && parentId.Value == Guid.Empty) throw new ArgumentException("ParentId invalid");
        // parent same-project validated at handler via DB; not here
        if (parentId.HasValue && progress < 0) { } // placeholder
        return new WorkItem(WorkItemId.New(), tenantId, projectId, parentId, title.Trim(), description, typeId, priorityId, criticalityId, ownerId, responsibleId, reviewerId, dueDate, estimatedHours, tags, progress);
    }

    public void ChangeStatus(int targetStatusId, IWorkItemTransitionPolicy policy, Guid actorId)
    {
        CheckRule(new TransitionIsAllowedRule(StatusId, targetStatusId, policy));
        var from = StatusId;
        StatusId = targetStatusId;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        if (targetStatusId == Enumerations.WorkItemStatus.Completed.Id)
            CompletedAt = DateTime.UtcNow;
        RaiseDomainEvent(new WorkItemStatusChangedDomainEvent(Id.Value, ProjectId, from, targetStatusId, actorId));
        if (targetStatusId == Enumerations.WorkItemStatus.Completed.Id)
            RaiseDomainEvent(new WorkItemCompletedDomainEvent(Id.Value, ProjectId));
    }

    public void Assign(Guid assigneeId, Guid assignerId)
    {
        // not completed check is via WorkItemNotCompletedRule in handler/policy, but also enforce here
        CheckRule(new WorkItemNotCompletedRule(StatusId, Enumerations.WorkItemStatus.Completed.Id));
        var old = ResponsibleId;
        ResponsibleId = assigneeId;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        if (old.HasValue && old.Value != assigneeId)
            RaiseDomainEvent(new WorkItemReassignedDomainEvent(Id.Value, old.Value, assigneeId));
        else
            RaiseDomainEvent(new WorkItemAssignedDomainEvent(Id.Value, assigneeId, old));
    }

    public void Reparent(Guid? newParentId)
    {
        // CheckRule for descendant is in handler (needs inspector)
        var old = ParentId;
        if (newParentId.HasValue && newParentId.Value == Id.Value) throw new BusinessRuleValidationException(new ReparentNoCycleRule(true));
        ParentId = newParentId;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new WorkItemReparentedDomainEvent(Id.Value, old, newParentId));
    }

    public void SetProgress(int percent)
    {
        _ = ProgressValue.FromPercent(percent);
        ProgressPercent = percent;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsOverdue(DateTime now) => DueDate.HasValue && DueDate.Value < now && StatusId != Enumerations.WorkItemStatus.Completed.Id;

    private sealed class TypeRequiredRule : IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "WorkItemType is required";
    }
}