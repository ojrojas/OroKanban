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
    public DateTime? StartedAt { get; private set; }
    public int ReopenedCount { get; private set; }
    public string? TagsJson { get; private set; } // stored as json or via owned? keep string for now, owned via EF owned?
    public IReadOnlyList<string> Tags => string.IsNullOrEmpty(TagsJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(TagsJson)!;
    public string? DeliverablesJson { get; private set; }
    public IReadOnlyList<string> Deliverables => string.IsNullOrEmpty(DeliverablesJson) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<string>>(DeliverablesJson)!;
    public string? Observations { get; private set; }

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

    public void ChangeStatus(int targetStatusId, IWorkItemTransitionPolicy policy, Guid actorId, DateTime? nowOverride = null)
    {
        CheckRule(new TransitionIsAllowedRule(StatusId, targetStatusId, policy));
        var from = StatusId;
        var wasReopen = StatusId == Enumerations.WorkItemStatus.Completed.Id && targetStatusId != Enumerations.WorkItemStatus.Completed.Id;
        StatusId = targetStatusId;
        Version++;
        var now = nowOverride ?? DateTime.UtcNow;
        UpdatedAt = now;
        // Reloj API: acumula ActualHours al salir de InProgress, reinicia al entrar
        if (from == Enumerations.WorkItemStatus.InProgress.Id && targetStatusId != Enumerations.WorkItemStatus.InProgress.Id)
        {
            if (StartedAt.HasValue)
            {
                var elapsed = now - StartedAt.Value;
                if (elapsed.TotalHours > 0) ActualHours = Math.Round(ActualHours + (decimal)elapsed.TotalHours, 2, MidpointRounding.AwayFromZero);
                StartedAt = null;
            }
        }
        if (from != Enumerations.WorkItemStatus.InProgress.Id && targetStatusId == Enumerations.WorkItemStatus.InProgress.Id)
        {
            StartedAt = now;
        }
        if (wasReopen) ReopenedCount++;
        if (targetStatusId == Enumerations.WorkItemStatus.Completed.Id)
        {
            CompletedAt = now;
            // Si estaba en InProgress, ya se acumuló arriba; si venía de InReview y nunca estuvo en InProgress con StartedAt, no hay acumulación
            StartedAt = null;
        }
        if (wasReopen)
            RaiseDomainEvent(new WorkItemReopenedDomainEvent(Id.Value, ProjectId, from, targetStatusId, actorId));
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

    public void Update(string title, string? description, int priorityId, int criticalityId, DateTime? dueDate, IReadOnlyList<string> tags, IReadOnlyList<string> deliverables, string? observations, int progress, decimal estimatedHours, decimal? actualHoursOverride = null)
    {
        CheckRule(new TitleRequiredRule(title));
        Title = title.Trim();
        Description = description;
        PriorityId = priorityId;
        CriticalityId = criticalityId;
        DueDate = dueDate?.ToUniversalTime();
        TagsJson = tags.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(tags);
        DeliverablesJson = deliverables.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(deliverables);
        Observations = observations;
        _ = ProgressValue.FromPercent(progress);
        ProgressPercent = progress;
        _ = Effort.FromHours(estimatedHours);
        EstimatedHours = estimatedHours;
        if (actualHoursOverride.HasValue)
        {
            _ = Effort.FromHours(actualHoursOverride.Value);
            ActualHours = actualHoursOverride.Value;
        }
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordTime(decimal actualHours, Guid actorId)
    {
        _ = Effort.FromHours(actualHours);
        ActualHours = actualHours;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new WorkItemActualTimeRecordedDomainEvent(Id.Value, ProjectId, actualHours, actorId));
    }

    public bool IsOverdue(DateTime now) => DueDate.HasValue && DueDate.Value < now && StatusId != Enumerations.WorkItemStatus.Completed.Id;

    private sealed class TypeRequiredRule : IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "WorkItemType is required";
    }
}