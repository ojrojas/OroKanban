# Data Model: Identity, Access and Organization

**Feature**: 003-identity-access-organization | **Date**: 2026-08-31

## Entities

### 1. ManagementRelationship (AggregateRoot, app-owned, BC-02)

Published by `Organization.Domain`, persisted in `organization.management_relationships` via `OrganizationDbContext : AppDbContextBase` (schema `organization`).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `ManagementRelationshipId : StronglyTypedId<Guid>` | PK, generated via `Guid.NewGuid()` on `AssignManager` | Root identifier |
| `TenantId` | `Guid` | FK to tenant (from `tenant_id` claim) | Tenant isolation — first gate in evaluator |
| `ManagerId` | `Guid` (`UserId` StronglyTypedId underlying) | FK to user (must exist in OroIdentityServer) | Manager side of edge |
| `SubordinateId` | `Guid` | FK to user, `CheckRule(ManagerCannotBeSubordinateRule)` | Subordinate side |
| `Type` | `ManagementType : Enumeration` | `Manager`/`Supervisor`/`Contributor` | Edge label |
| `OrganizationUnitId` | `OrganizationUnitId?` | nullable FK to `OrganizationUnit` | Scope within tenant org tree |
| `ValidFrom` / `ValidTo` | `DateTime?` | `ValidFrom <= ValidTo` when both set | Effective dates for handovers |
| `RowVersion` | `byte[]` | Concurrency token | Optimistic concurrency per constitution |
| `IsActive(now)` | computed | `ValidFrom <= now && (ValidTo is null || now <= ValidTo)` | Single active per subordinate/unit enforced via `Specification` + unique filtered index |

**Invariants**: `manager != subordinate`; no cycles (`SubtreeCannotContainManagerRule` via `GetAncestors(subordinateId)` — `managerId` must not be in ancestors); single active per subordinate per unit (configurable; enforced via `Specification` + `IBusinessRule` before insert).

**Events**: `ManagerAssignedToUser` (contains `ManagerId`, `SubordinateId`, `Type`, `UnitId`, `TenantId`), `ManagerRemovedFromUser`, `OrganizationUnitRestructured`.

**State transitions**: `AssignManager` → `Active` → `Revoke`/`Expired` (via `ValidTo`).

### 2. OrganizationUnit (AggregateRoot, BC-02)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `OrganizationUnitId : StronglyTypedId<Guid>` | PK | |
| `TenantId` | `Guid` | required | Tenant isolation |
| `ParentId` | `OrganizationUnitId?` | nullable, self-FK | Tree within tenant |
| `Name` | `string` | required, max 200 | |
| `HierarchyPath` | `HierarchyPath : ValueObject` | computed path e.g., `/Root/Division/Team` | For display and `IsInSubtree` pre-filter |
| `RowVersion` | `byte[]` | concurrency token | |

**Events**: `OrganizationUnitCreated`, `OrganizationUnitMoved` (oldParent, newParent, tenant).

### 3. ExplicitGrant (AggregateRoot)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `ExplicitGrantId : StronglyTypedId<Guid>` | PK | |
| `TenantId` | `Guid` | required | Tenant-scoped |
| `GranteeUserId` | `Guid` | FK to user | Who receives cross-branch access |
| `GrantedBy` | `Guid` | FK to user | Must be manager of resource owner's subtree or owner (checked before `GrantIssued`) |
| `ResourceType` | `string` | e.g., `Project`, `WorkItem`, `Document` | |
| `ResourceId` | `Guid` | FK to resource | |
| `Permission` | `PermissionCode : ValueObject` | e.g., `workitem.read` | |
| `ExpiresAt` | `DateTime?` | nullable — null means no expiry | |
| `RowVersion` | `byte[]` | concurrency token | |

**Behavior**: `IsExpired(now) => ExpiresAt != null && now > ExpiresAt`; `IsSatisfiedBy(request) => !IsExpired && tenant matches && grantee == actor && resource+permission match`. Events: `GrantIssued`, `GrantRevoked`.

### 4. Permission & RolePermissionMap (Catalog, seeded, BC-01)

| Entity | Field | Type | Notes |
|--------|-------|------|-------|
| `Permission` | `Code` | `PermissionCode : ValueObject` (e.g., `project.read`) | PK, `Description`, `Category` |
| `RolePermission` | `Role` | `string` (one of the 10 seeded roles) | Composite PK `(Role, PermissionCode)`, no hard-coded enum in evaluator |

Seeded via `Identity.Infrastructure/Seed/PermissionSeederHostedService` on first run; extensible by adding rows, no evaluator code change.

### 5. Value Objects

| VO | Fields | Purpose |
|----|--------|---------|
| `HierarchyPath` | `Segments: IReadOnlyList<string>` | Immutable path for display / pre-filter |
| `SubtreeScope` | `ManagerId: Guid, TenantId: Guid` | Filter for subtree `Specification<T>` |
| `PermissionCode` | `Value: string` (e.g., `workitem.assign`) | Permission identifier |
| `GrantScope` | `ResourceType: string, ResourceId: Guid, TenantId: Guid` | Grant boundary for `IsSatisfiedBy` |

### 6. Domain Services

| Service | Contract | Published By | Implemented By |
|---------|----------|--------------|----------------|
| `IManagementHierarchy` | `IsInSubtree(managerId, userId)`, `GetSubtree(managerId)`, `GetAncestors(userId)`, `GetCommonAncestor(a,b)` | `Organization.Contracts` (Shared Kernel) | `Organization.Infrastructure` (recursive CTE + Redis cache — research Decision 1/2) |
| `IAuthorizationEvaluator` | `CanActorPerform(actor, resource, permission, tenant, classification) → Result` | `Organization.Domain` | `Organization.Infrastructure` (composes tenant→permission→ownership→subtree→grant→membership→classification, research Decision 4) |
| `IProjectMembership` | `IsMember(userId, resourceId) → bool` | Consumed contract (not implemented here) | Stub `IProjectMembership` in `Organization.Infrastructure` for now; real implementation arrives with Projects spec |

## Relationships Overview

```
OrganizationUnit (1) ──< ManagementRelationship (many, via OrganizationUnitId + TenantId)
ManagementRelationship —publishes→ ManagerAssignedToUser → OrganizationHierarchyChangedIntegrationEvent → Redis invalidation
ExplicitGrant —— evaluated by —— IAuthorizationEvaluator ── uses ── IManagementHierarchy + IPermissionCatalog + IProjectMembership
IAuthorizationEvaluator ── emits on deny ── audit.authorization.denied (via outbox, append-only)
```

## Validation Rules (from spec)

- FR-001: No local login/password — `sub`/`tenant_id` from JWT only; verified by OIDC claim mapping test.
- FR-003: `manager != subordinate`, no cycles (ancestor check via `GetAncestors`), single active per subordinate/unit — all via `IBusinessRule` + `CheckRule`.
- FR-004: `IManagementHierarchy` is the only hierarchy query path; results cached in Redis with explicit invalidation on `OrganizationHierarchyChangedIntegrationEvent`.
- FR-005: Evaluator order tenant→permission→ownership→subtree→grant→membership→classification; deny reasons internal only.
- FR-006: Every list/search/dashboard composes `new SubtreeSpecification<T>(actorId, tenantId)` via `Specification<T>` before fetch.
- FR-007: Deny emits outbox audit with actor/resource/permission/tenant/correlationId.
