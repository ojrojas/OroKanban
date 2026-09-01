using BuildingBlocks.EventBus.Abstractions;

namespace Projects.Contracts.Events;

public sealed record ProjectCreatedIntegrationEvent(Guid ProjectId, Guid TenantId, string Name) : IntegrationEvent;
public sealed record ProjectMemberAddedIntegrationEvent(Guid ProjectId, Guid UserId, int RoleId) : IntegrationEvent;
public sealed record WorkItemCreatedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, int TypeId) : IntegrationEvent;
public sealed record WorkItemStatusChangedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, int FromId, int ToId, Guid ActorId) : IntegrationEvent;
public sealed record WorkItemAssignedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, Guid AssigneeId, Guid AssignerId) : IntegrationEvent;
public sealed record WorkItemReparentedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, Guid? OldParentId, Guid? NewParentId) : IntegrationEvent;
public sealed record DependencyAddedIntegrationEvent(Guid DependencyId, Guid DependentId, Guid PrincipalId, int TypeId, Guid TenantId) : IntegrationEvent;
public sealed record DependencyRemovedIntegrationEvent(Guid DependencyId) : IntegrationEvent;