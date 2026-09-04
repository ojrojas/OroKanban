using BuildingBlocks.Kernel.Domain.Events;

namespace Projects.Domain.Events;

public sealed record ProjectCreatedDomainEvent(Guid ProjectId, Guid TenantId) : DomainEvent;
public sealed record ProjectMemberAddedDomainEvent(Guid ProjectId, Guid UserId, int RoleId) : DomainEvent;
public sealed record ProjectMemberRemovedDomainEvent(Guid ProjectId, Guid UserId) : DomainEvent;
public sealed record ProjectStatusChangedDomainEvent(Guid ProjectId, int FromId, int ToId) : DomainEvent;
public sealed record MilestoneReachedDomainEvent(Guid ProjectId, Guid MilestoneId) : DomainEvent;

public sealed record WorkItemCreatedDomainEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, int TypeId) : DomainEvent;
public sealed record WorkItemStatusChangedDomainEvent(Guid WorkItemId, Guid ProjectId, int FromId, int ToId, Guid ActorId) : DomainEvent;
public sealed record WorkItemAssignedDomainEvent(Guid WorkItemId, Guid AssigneeId, Guid? OldAssigneeId) : DomainEvent;
public sealed record WorkItemReassignedDomainEvent(Guid WorkItemId, Guid OldAssigneeId, Guid NewAssigneeId) : DomainEvent;
public sealed record WorkItemReparentedDomainEvent(Guid WorkItemId, Guid? OldParentId, Guid? NewParentId) : DomainEvent;
public sealed record WorkItemCompletedDomainEvent(Guid WorkItemId, Guid ProjectId) : DomainEvent;
public sealed record WorkItemReopenedDomainEvent(Guid WorkItemId, Guid ProjectId, int FromId, int ToId, Guid ActorId) : DomainEvent;
public sealed record WorkItemActualTimeRecordedDomainEvent(Guid WorkItemId, Guid ProjectId, decimal ActualHours, Guid ActorId) : DomainEvent;
public sealed record DependencyAddedDomainEvent(Guid DependencyId, Guid DependentId, Guid PrincipalId, int TypeId) : DomainEvent;
public sealed record DependencyRemovedDomainEvent(Guid DependencyId) : DomainEvent;