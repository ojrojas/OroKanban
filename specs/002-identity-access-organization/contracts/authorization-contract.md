# Contract: IAuthorizationEvaluator + Subtree Specification

**Feature**: 003-identity-access-organization | **Date**: 2026-08-31
**Published by**: `Organization.Domain` (interface) | **Implemented by**: `Organization.Infrastructure`

## IAuthorizationEvaluator

Single domain service composing Golden Rule A (R5). Tenant check is first gate.

```csharp
namespace Organization.Domain.Services;

public sealed record AuthorizationRequest(
    Guid ActorUserId,
    Guid TenantId,
    string Permission, // PermissionCode.Value
    string ResourceType,
    Guid ResourceId,
    Guid? ResourceOwnerId,
    string? Classification,
    IReadOnlyList<string> ActorRoles
);

public sealed record AuthorizationResult(bool IsAllowed, DenyReason? DenyReason);
public enum DenyReason { TenantMismatch, MissingPermission, NotOwner, NotInSubtree, GrantExpired, NotMember, ClassificationDenied }

public interface IAuthorizationEvaluator
{
    Task<AuthorizationResult> EvaluateAsync(AuthorizationRequest request, CancellationToken ct);
}
```

### Evaluation Order (research Decision 4)

1. Tenant mismatch → `TenantMismatch`
2. `IPermissionCatalog.HasPermission(roles, permission)` → `MissingPermission`
3. Ownership (if `ResourceOwnerId` set and actor is owner) → allow if no further restrictions
4. `IManagementHierarchy.IsInSubtree(tenant, actor, owner)` → `NotInSubtree` if not in subtree and not self
5. `ExplicitGrant.IsSatisfiedBy` (including `ExpiresAt`) → success if grant covers request
6. `IProjectMembership.IsMember(actor, resourceId)` → success if member
7. Classification check → `ClassificationDenied` if classification forbids

- Deny reasons are logged and emitted as `audit.authorization.denied` via outbox (R7) but the caller receives only `Result.Failure(Error.Forbidden(...))` — never the `DenyReason` text.

### Subtree Specification

Every list/search/dashboard query composes the subtree before fetch (R6):

```csharp
// Example usage in a query handler
var subtree = await hierarchy.GetSubtreeAsync(tenantId, actorId, ct);
var spec = new SubtreeSpecification<WorkItem>(subtree, tenantId)
    .And(new WorkItemByStatusSpecification(status));
var items = await repository.ListAsync(spec, ct);
```

`SubtreeSpecification<T>` is a `Specification<T>` that filters by `ownerId IN subtree ∪ {actorId}` plus `tenant_id == TenantId`, combined via `And` with the resource query. It is the only authorization filter — never filter after fetching.

### Policy Probes (Application layer)

- `CanActorPerformQuery(AuthorizationRequest) → AuthorizationResult` — test/UI probe
- `WhoReportsToMeQuery(managerId) → IReadOnlyList<Guid>` — delegates to `GetSubtree`
- `GetSubtreeQuery(managerId) → IReadOnlyList<Guid>` — direct hierarchy probe

## Validation

- Tenant mismatch: `CanActorPerform` with `TenantId` != resource tenant → `Deny` + audit, no subtree work.
- Subtree success: Manager A whose subtree contains A1 → `CanActorPerform(A, ownedBy:A1, permission, tenant)` → `Allow`.
- Cross-branch isolation: Manager B not in A's subtree, no grant, not member → `Deny` + audit, caller's list is empty (not error-leaking).
- Grant expiry: `ExpiresAt` in past → `Deny` and `IsSatisfiedBy` false; future → `Allow` if otherwise authorized.
- Classification: `document.read` on `HighlyRestricted` without clearance → `ClassificationDenied`.
