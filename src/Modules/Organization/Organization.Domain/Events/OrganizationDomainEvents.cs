using BuildingBlocks.Kernel.Domain.Events;

namespace Organization.Domain.Events;

public sealed record ManagerAssignedToUser(
    Guid ManagementRelationshipId,
    Guid ManagerId,
    Guid SubordinateId,
    string Type,
    Guid? OrganizationUnitId,
    Guid TenantId
) : DomainEvent;

public sealed record ManagerRemovedFromUser(
    Guid ManagementRelationshipId,
    Guid ManagerId,
    Guid SubordinateId,
    Guid TenantId
) : DomainEvent;

public sealed record OrganizationUnitCreated(
    Guid OrganizationUnitId,
    Guid TenantId,
    string Name,
    Guid? ParentId
) : DomainEvent;

public sealed record OrganizationUnitMoved(
    Guid OrganizationUnitId,
    Guid TenantId,
    Guid? OldParentId,
    Guid? NewParentId
) : DomainEvent;

public sealed record GrantIssued(
    Guid ExplicitGrantId,
    Guid GranteeUserId,
    string ResourceType,
    Guid ResourceId,
    string Permission,
    Guid TenantId,
    DateTime? ExpiresAt
) : DomainEvent;

public sealed record GrantRevoked(
    Guid ExplicitGrantId,
    Guid TenantId
) : DomainEvent;
