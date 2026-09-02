using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Rules;

using Projects.Domain.Events;
using Projects.Domain.Ids;

namespace Projects.Domain.Aggregates;

public sealed class Project : AggregateRoot<ProjectId>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ManagerId { get; private set; }
    public int StatusId { get; private set; }
    public int PriorityId { get; private set; }
    public int CriticalityId { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? DueDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private readonly List<ProjectMember> _members = [];
    public IReadOnlyList<ProjectMember> Members => _members.AsReadOnly();

    private readonly List<Milestone> _milestones = [];
    public IReadOnlyList<Milestone> Milestones => _milestones.AsReadOnly();

    private Project() { }

    private Project(ProjectId id, Guid tenantId, string name, Guid ownerId, Guid managerId, int statusId, int priorityId, int criticalityId, DateTime? startDate, DateTime? dueDate, string? description)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        Description = description;
        OwnerId = ownerId;
        ManagerId = managerId;
        StatusId = statusId;
        PriorityId = priorityId;
        CriticalityId = criticalityId;
        StartDate = startDate;
        DueDate = dueDate;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;

        // manager is implied member? caller adds explicitly but we also keep list
        RaiseDomainEvent(new ProjectCreatedDomainEvent(id.Value, tenantId));
    }

    public static Project Create(Guid tenantId, string name, Guid ownerId, Guid managerId, int statusId, int priorityId, int criticalityId, DateTime? startDate, DateTime? dueDate, string? description)
    {
        CheckRule(new Rules.TitleRequiredRule(name));
        if (startDate.HasValue && dueDate.HasValue && startDate > dueDate)
            throw new BusinessRuleValidationException(new DueDateRule());

        var id = ProjectId.New();
        var p = new Project(id, tenantId, name.Trim(), ownerId, managerId, statusId, priorityId, criticalityId, startDate, dueDate, description);
        // auto-add owner/manager as members if not same?
        return p;
    }

    public void AddMember(Guid userId, int roleId)
    {
        if (_members.Any(m => m.UserId == userId))
            throw new BusinessRuleValidationException(new DuplicateMemberRule());
        var m = new ProjectMember(Guid.NewGuid(), userId, roleId);
        _members.Add(m);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProjectMemberAddedDomainEvent(Id.Value, userId, roleId));
    }

    public void RemoveMember(Guid userId)
    {
        var m = _members.FirstOrDefault(x => x.UserId == userId);
        if (m is null) throw new BusinessRuleValidationException(new MemberNotFoundRule());
        _members.Remove(m);
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProjectMemberRemovedDomainEvent(Id.Value, userId));
    }

    public void ChangeStatus(int newStatusId)
    {
        var old = StatusId;
        StatusId = newStatusId;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new ProjectStatusChangedDomainEvent(Id.Value, old, newStatusId));
    }

    private sealed class DueDateRule : IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "StartDate must be <= DueDate";
    }
    private sealed class DuplicateMemberRule : IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "Member already exists";
    }
    private sealed class MemberNotFoundRule : IBusinessRule
    {
        public bool IsBroken() => true;
        public string Message => "Member not found";
    }
}

public sealed class ProjectMember
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int RoleId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private ProjectMember() { }
    public ProjectMember(Guid id, Guid userId, int roleId)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
        JoinedAt = DateTime.UtcNow;
    }
}

public sealed class Milestone
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = default!;
    public DateTime? DueDate { get; private set; }
    public bool IsReached { get; private set; }
    public DateTime? ReachedAt { get; private set; }

    private Milestone() { }
    public Milestone(Guid id, string title, DateTime? dueDate)
    {
        Id = id;
        Title = title;
        DueDate = dueDate;
    }

    public void MarkReached()
    {
        IsReached = true;
        ReachedAt = DateTime.UtcNow;
    }
}