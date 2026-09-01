# Contract: IManagementHierarchy (Shared Kernel) + Hierarchy Event

**Feature**: 003-identity-access-organization | **Date**: 2026-08-31
**Published by**: `Organization.Contracts` | **Implemented by**: `Organization.Infrastructure`

## IManagementHierarchy

Shared Kernel contract — the **only** way other bounded contexts query hierarchy (R4). Tenant-scoped.

```csharp
namespace Organization.Contracts;

public interface IManagementHierarchy
{
    Task<bool> IsInSubtreeAsync(Guid tenantId, Guid managerId, Guid userId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetSubtreeAsync(Guid tenantId, Guid managerId, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetAncestorsAsync(Guid tenantId, Guid userId, CancellationToken ct);
    Task<Guid?> GetCommonAncestorAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct);
}
```

### Storage (research Decision 1)

Adjacency list `organization.management_relationships(manager_id, subordinate_id, type, valid_from/to, organization_unit_id, tenant_id)` + indexes on `(tenant_id, manager_id)` and `(tenant_id, subordinate_id)`. Queries are `WITH RECURSIVE` CTEs over active rows (`ValidFrom <= now && (ValidTo is null || now <= ValidTo)`). Per-module schema `organization`; `RowVersion` concurrency.

### Cache (research Decision 2)

- Keys: `hierarchy:{tenant}:{managerId}:subtree` and `hierarchy:{tenant}:{managerId}:{userId}:isIn`
- TTL 5 min, explicit delete on `OrganizationHierarchyChangedIntegrationEvent`
- Fallback to CTE on Redis miss/unavailable (log warning, no auth bypass)

## OrganizationHierarchyChangedIntegrationEvent

Already seeded in `Organization.Contracts/Events/OrganizationHierarchyChangedIntegrationEvent.cs` by 002; extended here with `TenantId` if not already present.

```csharp
public sealed record OrganizationHierarchyChangedIntegrationEvent(
    Guid ActorUserId,
    Guid TargetUserId,
    string ChangeType, // ManagerAssigned | ManagerRemoved | UnitMoved
    Guid? OrganizationUnitId,
    Guid TenantId,
    DateTime ChangedAtUtc
) : IntegrationEvent;
```

- Published via outbox from `ManagementRelationship` domain events (`ManagerAssignedToUser`, etc.)
- Consumers: `HierarchyCacheInvalidator` (deletes affected manager + ancestor keys), future `Audit` consumer, any module caching subtree.

## Validation

- `AssignManager(A→B)` then `GetAncestors(B)` contains `A`; `IsInSubtree(A, B)` true; `IsInSubtree(B, A)` false.
- Cycle attempt `C→A` where `A` in `GetAncestors(C)` is rejected with `Error.Validation` and no row inserted.
- After `ManagerAssignedToUser`, the next `GetSubtree(managerId)` reflects the new member without restart (cache invalidation).
