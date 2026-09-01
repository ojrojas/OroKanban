# Feature Specification: Identity, Access and Organization

**Feature Branch**: `003-identity-access-organization`

**Created**: 2026-08-31

**Status**: Draft

**Input**: User description: "Identity, Access and Organization — BC-01 Identity & Access + BC-02 Organization (Supporting), Depends on SPEC-001 Foundation. Objective: Implement authentication (consumed) and hierarchical authorization (app-owned) with recursive management hierarchy enforcing Golden Rule A. Requirements R1 identity consumed via oroidentityserver OIDC, R2 app-owned permission catalog and role→permission mapping, R3 ManagementRelationship aggregate with cycle prevention, R4 IManagementHierarchy subtree evaluation (Shared Kernel), R5 single AuthorizationEvaluator composing Golden Rule A, R6 cross-branch isolation via subtree Specification, R7 audit of authorization failures."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Manager establishes and maintains reporting hierarchy (Priority: P1)

As a manager (or Root Manager), I want to assign subordinates and other managers to my reporting structure and have the system prevent cycles, so that the organizational hierarchy remains a valid tree and can be used for authorization.

**Why this priority**: The hierarchy is the foundation of Golden Rule A and every later authorization decision (BC-02 Shared Kernel). Without a cycle-free, arbitrary-depth ManagementRelationship, subtree evaluation and cross-branch isolation cannot be enforced. Blocks User Stories 2 and 3.

**Independent Test**: Can be fully tested by creating users A, B, C, then calling AssignManager to build A→B→C, and verifying that attempting to assign C→A is rejected with an error and no state change, while AssignManager A→B succeeds, raises ManagerAssignedToUser, and appears in GetAncestors(C) and IsInSubtree(A, C).

**Acceptance Scenarios**:

1. **Given** users Alice (manager) and Bob, **When** Alice assigns Bob as a Contributor in the same organization unit, **Then** the relationship is created, a ManagerAssignedToUser event is published, and Bob appears in Alice's subtree.
2. **Given** hierarchy A→B→C exists, **When** a request tries to make C the manager of A, **Then** the domain rejects it with a validation error (cycle) and the hierarchy remains unchanged.
3. **Given** Alice tries to assign herself, **When** AssignManager is called with ManagerId equals SubordinateId, **Then** the request is rejected as manager ≠ subordinate.
4. **Given** a hierarchy change, **When** the integration event is published, **Then** cached subtree results for affected managers are invalidated (verified via a follow-up GetSubtree query).

---

### User Story 2 - System enforces hierarchical authorization on resource access (Priority: P1)

As an authenticated user requesting tasks or organization resources, I want the system to decide access by composing identity, roles, permissions, tenant, subtree, project membership, ownership, and classification, so that I see only what my position and grants allow and cross-branch data is never leaked.

**Why this priority**: Implements Golden Rule A (Principles II, VII, XV, XIX) and the core business rule that RBAC alone is insufficient. This is the user-visible enforcement of Story 1's hierarchy; without it, the hierarchy has no security effect. Same P1 as Story 1 — together they deliver the secure organization.

**Independent Test**: Can be tested with a frozen hierarchy (Root→ManagerA→{A1, A2, M-A1→A1's reports} and ManagerB in another branch) and three queries: Manager A querying tasks returns subtree + explicit-grant + project-member items; Manager B querying A's items returns empty (not error-leaking); a Contributor querying outside subtree without grant returns empty.

**Acceptance Scenarios**:

1. **Given** Manager A whose subtree contains A1, A2, and M-A1's transitive reports, **When** A queries tasks, **Then** the result set equals items owned by subtree members plus items covered by explicit grants plus items where A has project membership — no other items.
2. **Given** Manager B in a different branch with no grant or shared membership, **When** B queries a task owned by A1, **Then** the result set is empty (absent, not a 403 that reveals existence) and no error message leaks the resource's presence.
3. **Given** a user with role Contributor and permission `workitem.read`, **When** the AuthorizationEvaluator composes the request with that permission, tenant, subtree, and classification inputs, **Then** the decision is `Allow` only if all required inputs pass; otherwise the deny reason is logged and audited but the caller receives only a generic denial.
4. **Given** a task list query, **When** it executes, **Then** a subtree Specification is composed with the resource query before data is fetched (never filter after fetching).

---

### User Story 3 - Auditor and manager observe authorization decisions and hierarchy changes (Priority: P2)

As an auditor or manager, I want authorization failures to be audited and hierarchy changes to be observable via events, so that access decisions are traceable and cached views stay consistent.

**Why this priority**: Satisfies Principles VIII (Everything Auditable) and the cache-invalidation part of R4. Provides accountability and correctness after the core hierarchy and evaluator exist, but is only observable once Stories 1 and 2 produce decisions and changes.

**Independent Test**: Can be tested by triggering a deny decision (e.g., B accessing A's task) and then querying the audit log for an entry containing actor, resource, permission, tenant, and correlation ID; and by changing a relationship and checking that a subsequent GetSubtree repopulates from storage rather than stale Redis.

**Acceptance Scenarios**:

1. **Given** any deny decision, **When** evaluation completes, **Then** an audit event (actor, action `authorization.denied`, resource type/id, permission, tenant, correlation ID) is persisted via the outbox within the same transaction.
2. **Given** a hierarchy change (ManagerAssignedToUser), **When** the `OrganizationHierarchyChangedIntegrationEvent` is consumed, **Then** Redis keys for `GetSubtree(managerId)` and `IsInSubtree(managerId, userId)` for affected managers are deleted and the next query reflects the new hierarchy.
3. **Given** an explicit grant with an expiration, **When** the grant expires, **Then** `CanActorPerform` for that user × resource × permission returns `Deny` and `IsSatisfiedBy` for the grant's time window evaluates to false.
4. **Given** who-reports-to-me is queried, **When** the manager has no subordinates, **Then** the result is an empty list (not null) and no cache entry is poisoned.

---

### Edge Cases

- What happens when a subordinate already has an active manager in the same unit and a second AssignManager is attempted? The domain enforces single active manager per subordinate per unit (configurable) and rejects the duplicate with a conflict error unless the existing relationship's effective dates allow a handover.
- What happens when an explicit grant is issued for a resource the granter does not control? The evaluator checks granter authority (must be manager of the resource's owner subtree or owner) before issuing; otherwise the grant is denied.
- What happens when tenant_id from the OIDC token differs from the resource's tenant? The evaluator denies immediately on tenant mismatch before any subtree or permission checks — tenant isolation is the first gate.
- What happens when Redis is unavailable? Subtree evaluation falls back to storage (recursive CTE path) and still returns correct results; the cache miss is logged but does not cause an authorization failure or bypass.
- What happens when a management cycle is attempted via a multi-step reorganization (A→B, B→C, C→A across three separate commands)? The third command's CheckRule detects the ancestor relationship via `GetAncestors(C)` and rejects, preserving the invariant after each transaction.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST delegate authentication entirely to `oroidentityserver` via OIDC authorization-code + refresh flows, validating JWTs against the discovery endpoint, and MUST NOT store passwords, provide a login UI, or issue tokens. Claims consumed on every request are `sub`, email, name, roles, and `tenant_id`; `tenant_id` is propagated as tenant context for all authorization decisions.
- **FR-002**: System MUST own permission evaluation through an extensible, seeded (not hard-coded) permission catalog covering `project.read/create/update/delete`, `workitem.read/create/assign/update/complete`, `document.read/upload/classify/version/approve`, `ai.execute/review/approve`, `audit.read`, `organization.manage`, plus a seeded role→permission mapping for roles `RootManager`, `Manager`, `Supervisor`, `Contributor`, `Reviewer`, `Auditor`, `DocumentManager`, `ProjectManager`, `AIReviewer`, `Administrator`. Roles are defined in OroIdentityServer; all mapping and evaluation logic resides in OroKanban.
- **FR-003**: System MUST implement `ManagementRelationship` as an app-owned aggregate root (`ManagementRelationshipId` StronglyTypedId) with fields `ManagerId → SubordinateId`, `Type` (Manager/Supervisor/Contributor), effective dates, and `OrganizationUnit` scope, supporting arbitrary depth where a manager may manage other managers, and MUST enforce invariants manager ≠ subordinate, no cycles via `SubtreeCannotContainManagerRule` (`CheckRule`), and single active manager per subordinate per unit (configurable) — publishing `ManagerAssignedToUser`, `ManagerRemovedFromUser`, and `OrganizationUnitRestructured` as domain events.
- **FR-004**: System MUST provide a Shared Kernel contract `IManagementHierarchy` published by BC-02 exposing `IsInSubtree(managerId, userId)`, `GetSubtree(managerId)`, `GetAncestors(userId)`, and `GetCommonAncestor(a, b)`, whose storage strategy (recursive CTE vs. closure table vs. ltree per draft/discovery ADR) is decided via ADR before implementation, whose results are cacheable in Redis and explicitly invalidated on `OrganizationHierarchyChangedIntegrationEvent`, and whose contract is the only way other bounded contexts query hierarchy.
- **FR-005**: System MUST evaluate every protected action through a single domain service `IAuthorizationEvaluator` that composes Golden Rule A — `Identity + Role/Permission + Organization (tenant) + Subtree + Project Membership + Ownership + Classification → Decision` — with explicit policy inputs; deny reasons are distinguishable internally, logged, and audited, but MUST NOT be leaked to callers (caller receives only allow/deny).
- **FR-006**: System MUST enforce cross-branch isolation: a subordinate SHALL NOT automatically access resources in another branch; cross-branch access requires an explicit `ExplicitGrant` (user × resource × permission × expiresAt, with `GrantIssued`/`GrantRevoked` events) or project membership, and every list/search/dashboard query MUST compose a subtree `Specification<T>` with the resource query before fetching data.
- **FR-007**: System MUST audit every authorization deny decision by emitting an audit event containing actor, action `authorization.denied`, resource type/id, permission, tenant, result, and correlation ID via the transactional outbox, so that the audit trail is traceable per Principle VIII.
- **FR-008**: System MUST provide `OrganizationUnit` aggregate (tree within a tenant) with `OrganizationUnitCreated`/`OrganizationUnitMoved` events, and `ExplicitGrant` aggregate (`ExplicitGrantId` StronglyTypedId) with expiry-aware `IsSatisfiedBy` checks; grant expiry is evaluated on every `CanActorPerform` call.
- **FR-009**: System MUST expose application operations `AssignManager`, `MoveOrganizationUnit`, `IssueExplicitGrant`, `RevokeExplicitGrant` (commands with `CheckRule` + domain events + cache invalidation) and queries `GetSubtree`, `WhoReportsToMe`, `CanActorPerform` as policy probes usable by tests and UI.

### Key Entities

- **ManagementRelationship** (BC-02, app-owned): Represents ManagerId → SubordinateId with Type, effective dates, OrganizationUnit scope. Attributes: id (StronglyTypedId), manager/subordinate user ids, type enum, validFrom/validTo, unit id, tenant id. Invariants: no self-reference, no cycles, single active per subordinate/unit, org-scope consistency.
- **OrganizationUnit** (BC-02): Represents a node in the tenant's org tree. Attributes: id, parent id (nullable), tenant id, name, hierarchy path. Events: Created, Moved.
- **ExplicitGrant** (app-owned): Represents a cross-branch exception for a single user × resource × permission with expiry. Attributes: id, grantee user id, resource type/id, permission code, grantedBy, expiresAt, tenant id. Behavior: `IsExpired(now)` and `IsSatisfiedBy(request)`.
- **PermissionCatalog & Role→Permission Mapping**: Extensible catalog (not hard-coded) seeded with permissions and role mappings for the 10 initial roles. Attributes: permission code, description, classification; role name, permission set.
- **HierarchyPath / SubtreeScope / PermissionCode / GrantScope** (value objects): Immutable descriptors for paths, scope filters, permission identifiers, and grant boundaries used by specifications and domain services.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authenticated user whose OIDC token was issued by `oroidentityserver` can call a protected OroKanban endpoint and the service sees `sub`, roles, and `tenant_id` without any local login or password check — verifiable via a token issued from the discovery endpoint succeeding and a local-login attempt failing.
- **SC-002**: Given a hierarchy Root→ManagerA→{A1, A2, M-A1→their reports} and ManagerB in another branch, ManagerA's task query returns exactly the subtree + explicit-grant + project-member set (no more, no less), while ManagerB's query for the same resource set returns empty — both results obtained in under 500 ms for a 1,000-user seeded hierarchy.
- **SC-003**: Attempting to create a cycle (C→A where A is an ancestor of C) is rejected with a validation error in under 100 ms and leaves the persisted hierarchy unchanged — verifiable by a follow-up `GetAncestors(C)` still not containing A.
- **SC-004**: Every deny decision produces an audit entry containing actor, resource type/id, permission, tenant, and correlation ID within the same outbox transaction — verifiable by querying the audit store after a denied `CanActorPerform` call.
- **SC-005**: After a hierarchy change, the next `GetSubtree(managerId)` reflects the new structure (cache was invalidated via `OrganizationHierarchyChangedIntegrationEvent`) — verifiable by changing a relationship, then reading subtree and seeing the updated member count without restarting the service.

## Assumptions

- `oroidentityserver` is already running as a Podman container and reachable at the Authority configured for the environment (per `draft/oroidentityserver-specification.md` and `draft/discovery/000-repository-catalog.md`); client registration for `authorization_code` + `refresh_token` grants has been completed out of band.
- The permission catalog and role→permission seed are the 10 roles listed in R2; additional permissions/roles may be added later without code changes to the evaluator.
- Hierarchy storage strategy (recursive CTE, closure table, or ltree) will be chosen via ADR before coding `IManagementHierarchy`; until then, the contract and domain invariants are storage-agnostic and tests use `IsSatisfiedBy` + in-memory subtree evaluation.
- Single active manager per subordinate per unit is the default invariant; the requirement notes it is configurable, so a future configuration may relax it to multiple concurrent managers.
- Project Membership for cross-branch access will be defined in a later spec (Projects bounded context); this spec only consumes the membership check via an interface/contract, it does not implement project membership itself.

