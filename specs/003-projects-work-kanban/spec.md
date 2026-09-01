# Feature Specification: Projects, Work Items and Kanban

**Feature Branch**: `003-projects-work-kanban`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "SPEC-003 — Projects, Work Items and Kanban **Bounded Context**: BC-03 Projects & Work Management (Core) · **Depends on**: SPEC-002 Objective: Implement the core project and Kanban experience over hierarchical, stateful work items with validated transitions and dependencies. Requirements R1 Project aggregate, R2 Work item aggregate typed by Enumeration, R3 Hierarchy via ParentId with validated reparenting, R4 State machine with allowed-transition map and domain CheckRule, R5 Dependencies with cycle detection and CircularDependencyRule, R6 Assignment with subtree/project membership validation, R7 Kanban projection as read-model query side never mutating state. Domain Model: Project, WorkItem, WorkItemDependency aggregates; Value Objects WorkItemStatus/Priority/Criticality/Effort/Tag/DueDate/ProgressValue; Domain services IDependencyCycleDetector, IAssignmentPolicy, IWorkItemTransitionPolicy. Application Layer: CreateProject, AddProjectMember, CreateWorkItem, ReparentWorkItem, ChangeWorkItemStatus, AssignWorkItem, AddDependency, RemoveDependency, CompleteWorkItem; Queries GetKanbanBoard, GetWorkItemDetail, GetMyTasks, GetTeamTasks."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Project and work item creation with hierarchy (Priority: P1)

As a project manager within an authorized organizational scope, I want to create a project, add participants with roles, and create typed work items (Epic/Feature/Task/Subtask) in a hierarchy, so that work is organized, typed, and traceable from the start.

**Why this priority**: Delivers the core data foundation for all later workflows. Without Project and hierarchical WorkItem aggregates, state transitions, dependencies, assignment, and the Kanban board have nothing to operate on. Satisfies R1, R2, R3 and Constitution Principle VI (domain rules).

**Independent Test**: Can be fully tested by creating a Project (owner + manager), adding two ProjectMembers, creating an Epic under the project, then a Feature under the Epic, then a Task under the Feature, verifying each creation raises its domain event via outbox, persists with version 1, and that ParentId linkage is stored and queryable via GetWorkItemDetail.

**Acceptance Scenarios**:

1. **Given** an authenticated manager within scope, **When** they create a project with identity, owner, manager, dates, status, priority, and criticality, **Then** the project is persisted, a `ProjectCreated` event is emitted to the outbox, and the manager appears as a member.
2. **Given** an existing project, **When** the manager adds a participant via `AddProjectMember` with a valid role, **Then** the member is added, `ProjectMemberAdded` is emitted, and membership is visible to Golden Rule A for later assignment checks.
3. **Given** a project with at least one member, **When** a manager within scope creates a work item (title, description, type, status=Backlog, priority, criticality, owner/responsible, dates, effort estimate, tags), **Then** it is persisted with version 1, `WorkItemCreated` is in the outbox, and it appears in GetWorkItemDetail and GetKanbanBoard(Backlog column).
4. **Given** a hierarchy Epic → Feature already exists, **When** a Task is created with ParentId pointing to the Feature, **Then** the parent link is stored, the Task is a child in hierarchy queries, and re-parenting via bare field update is not permitted (only via `ReparentWorkItem` command).
5. **Given** a request to create a work item where the taxonomy type value is not a configured `Enumeration` value, **When** the command is executed, **Then** it is rejected with a validation error and no entity is created.

---

### User Story 2 - Kanban board and validated state transitions (Priority: P1)

As a team member viewing the Kanban board, I want to drag a work item between columns and have the system validate the transition, enforce authorization, and either move the card or reject with a clear error while the board re-renders unchanged.

**Why this priority**: Core of the "Kanban experience" and R4/R7. The board is the primary UI for status changes; invalid transitions must be domain-rejected, never silently accepted. Same P1 as Story 1 because without transitions the work items are static.

**Independent Test**: Can be fully tested by seeding a work item in `Backlog`, issuing `ChangeWorkItemStatus` to `Planned` (allowed) and verifying `WorkItemStatusChanged` + audit + board column move, then issuing `ChangeWorkItemStatus` from `Backlog` directly to `Completed` (disallowed) and verifying rejection, no state change, and board retains card in Backlog.

**Acceptance Scenarios**:

1. **Given** a work item in `Backlog`, **When** an authorized user drags it to `Planned` (allowed transition `Backlog → Planned`), **Then** the domain executes `CheckRule(new TransitionIsAllowedRule(Backlog, Planned))`, passes authorization via SPEC-002 evaluator, publishes `WorkItemStatusChanged`, creates an audit entry, and the board query now shows the item in the `Planned` column.
2. **Given** a work item in `Backlog`, **When** a drag/drop issues `ChangeWorkItemStatus` to `Completed` (no allowed path), **Then** the domain rejects with a validation error (`Transition not allowed`), no event is emitted, no audit for status change is created (authorization denial is audited per SPEC-002), and `GetKanbanBoard` still shows the item in `Backlog`.
3. **Given** a work item in `In Progress`, **When** it is moved to `Blocked`, **Then** the transition succeeds and `WorkItemBlocked` may be raised if dependencies are considered; given it is in `Blocked`, **When** moved to `In Review`, **Then** the bidirectional `Blocked ↔ In Review` rule allows it (verified via `WorkItemStatus` transition map).
4. **Given** a work item in `Completed`, **When** an authorized user attempts to reopen it, **Then** the outcome follows the configured reopen rules (Completed → In Progress or Backlog only if policy allows; otherwise rejected with explanation) — reopen is a domain operation, not a bare status edit.
5. **Given** an unauthorized user attempts any transition, **When** `ChangeWorkItemStatus` is evaluated, **Then** the SPEC-002 `IAuthorizationEvaluator` denies, an authorization-denied audit entry is created, no status event is emitted, and the caller receives only a generic denial (deny reasons not leaked).

---

### User Story 3 - Hierarchy reparenting and dependency management with cycle prevention (Priority: P2)

As a project lead, I want to reorganize work by reparenting items and linking dependencies between items, with the system preventing cycles and validating the new structure, so that the plan stays consistent.

**Why this priority**: R3 and R5. Hierarchy reorganization and dependency linking are essential planning operations that depend on Stories 1-2 existing. Cycle prevention is a critical invariant that must be enforced domain-side.

**Independent Test**: Can be fully tested by creating items A, B, C with chain A blocks B, B blocks C (or DependsOn), verifying the chain persists, then attempting C→A dependency and confirming `CircularDependencyRule` rejection with no graph change; separately, reparent a Task from Feature-1 to Feature-2 via `ReparentWorkItem` and verify `WorkItemReparented` + new parent in detail query; attempt to reparent an item under its own descendant and verify rejection.

**Acceptance Scenarios**:

1. **Given** work items A → B → C dependency chain (A Blocks B, B Blocks C), **When** adding dependency C → A (any blocked-type), **Then** `IDependencyCycleDetector` traverses the graph, `CircularDependencyRule` fails, command returns validation error, and no `DependencyAdded` event is emitted.
2. **Given** an item Task-1 parented to Epic-1, **When** `ReparentWorkItem(Task-1, newParent=Epic-2)` is issued by an authorized actor, **Then** the domain validates the new parent is in same project, not a descendant (no hierarchy cycle), authorized, emits `WorkItemReparented`, and `GetWorkItemDetail` shows the new ParentId.
3. **Given** an item Epic-A, **When** attempting to reparent Epic-A under one of its own descendants (Feature-A1), **Then** the domain rejects with hierarchy-cycle error and the graph is unchanged.
4. **Given** a `WorkItemDependency` with type `Blocks`, **When** valid, **Then** the dual interpretation `BlockedBy` is derivable for the principal; `RelatedTo` never affects `Blocked` derivation.
5. **Given** unresolved `BlockedBy`/`DependsOn` links exist for a work item, **When** the item's blocked-state is evaluated, **Then** `Blocked` status is derivable (partially blocked) per domain service, but direct status mutation to `Blocked` still requires a valid transition.

---

### User Story 4 - Assignment with hierarchical authorization (Priority: P2)

As a manager, I want to assign work items to members where the system checks that the assignee is in my subtree or shares project membership, is active, and the item is not completed, so that assignments respect organizational boundaries.

**Why this priority**: R6 and Golden Rule A. Assignment is the collaboration entry point for execution; its authorization combines hierarchy (SPEC-002 `IManagementHierarchy`) and project membership (R1). P2 because it depends on project membership and hierarchy but is independent of dependency logic.

**Independent Test**: Can be fully tested by seating a hierarchy Root→MgrA→{A1, A2} and a separate branch MgrB, plus a project where MgrA and A1 are members but MgrB is not; assign from MgrA to A1 (success → `WorkItemAssigned`), assign from MgrB to A1's item without project membership (deny + audited), and assign to a completed item (reject).

**Acceptance Scenarios**:

1. **Given** work item WI is not completed and assignee A1 is in assigner MgrA's subtree, **When** MgrA issues `AssignWorkItem(WI, A1)`, **Then** `IAssignmentPolicy` passes, `WorkItemAssigned` is emitted, audit event and notification integration event are produced, and GetMyTasks for A1 includes WI.
2. **Given** WI is not completed and assignee is not in assigner's subtree but shares project membership with assigner, **When** assignment is issued, **Then** it is allowed (membership satisfies Golden Rule A feed from R1).
3. **Given** assigner outside assignee's subtree and without shared project membership, **When** `AssignWorkItem` executes, **Then** it is denied, no assignment event is emitted, an audited authorization-denied entry is created, and the caller receives a generic denial.
4. **Given** assignee is inactive (deactivated user), **When** assignment is attempted, **Then** it is rejected with validation error regardless of hierarchy or membership.
5. **Given** WI status is `Completed`, **When** any assignment is attempted, **Then** it is rejected (`work item is not completed` rule) and no event is emitted.

---

### User Story 5 - Kanban projection, filtering, and visualization (Priority: P2)

As a team member, I want the Kanban board to show columns by status, swimlanes by assignee or epic, filters/sorting, progress and criticality visualization, and overdue indicators, as a pure read model that never mutates state, so that I can plan and triage without risking unintended changes.

**Why this priority**: R7. The board's read-model nature is a constitutional correctness property (Principle XIV, XVI). Users need filtering and visual cues to act; board mutations through queries must be impossible by design.

**Independent Test**: Can be fully tested by seeding items across statuses/assignees/epics/due dates, calling `GetKanbanBoard(projectId, filters)` with assignee filter and verifying only matching items in correct columns; calling with epic swimlane grouping and verifying grouping; verifying `DueDate` past today shows overdue indicator and criticality color mapping; and confirming no board endpoint accepts a write operation.

**Acceptance Scenarios**:

1. **Given** a project with work items in `Backlog`, `Planned`, `In Progress`, `Blocked`, `In Review`, `Completed`, **When** `GetKanbanBoard(projectId)` is queried, **Then** items are grouped by `WorkItemStatus` column, each column shows count and ordered per sort criteria, and pagination/filter metadata is returned.
2. **Given** the board query with `swimlane=assignee`, **When** executed, **Then** items are grouped into swimlanes by responsible/assignee; with `swimlane=epic`, grouping is by root Epic ancestor.
3. **Given** filters for status, assignee, epic, priority, criticality, tags, due-date range, **When** any filter combination is applied, **Then** the projection returns only items satisfying all filters, with authorization filtering (SPEC-002 subtree Specification) applied before fetch, and results remain paginated and sorted.
4. **Given** work items with progress, criticality, and due dates, **When** board rows are rendered, **Then** progress bar (from `ProgressValue`), criticality badge color, and overdue indicator (`DueDate < today AND status != Completed`) are visible per item.
5. **Given** any board interaction, **When** a user attempts to mutate state through the board query path, **Then** no mutation occurs — the board endpoint is read-only and the only way to change status/assignment is via commands (`ChangeWorkItemStatus`, `AssignWorkItem`) issued by drag/drop or explicit action.

---

### User Story 6 - Observability: audit, concurrency, and board round-trip (Priority: P3)

As an auditor or user, I want every important change audited, concurrent edits to conflict safely, and the Kanban drag/drop to round-trip from command to refreshed projection, so that changes are traceable and reliable.

**Why this priority**: Ties together Principles VIII (auditable), XVI (contracts + concurrency), and the TDD E2E requirement. It is P3 because it validates rather than delivers the core domain, but it is still mandatory for Definition of Done.

**Independent Test**: Can be fully tested by performing two concurrent `UpdateWorkItem` saves with same original version and verifying the second receives a concurrency Error; performing any status/assignment change and querying audit log for append-only entry with actor, action, resource, version; performing a drag/drop E2E (board query → command → board re-query) and verifying updated column.

**Acceptance Scenarios**:

1. **Given** two concurrent updates to the same work item both starting from version N, **When** both attempt to save, **Then** the first succeeds (version → N+1, outbox event), the second receives a concurrency `Error` (optimistic version mismatch, HTTP 409), and no silent overwrite occurs.
2. **Given** any status or assignment change commits, **When** the transaction completes, **Then** an append-only audit entry (actor, action, resource type/id, before/after, correlation ID, tenant) is persisted via outbox, and where relevant a notification integration event is emitted.
3. **Given** a Kanban drag/drop from `In Progress` to `In Review` (valid), **When** `ChangeWorkItemStatus` is issued, **Then** the full chain board query → command → domain validation → event → outbox → projection refresh completes and the subsequent `GetKanbanBoard` shows the item in `In Review`.
4. **Given** any API list query (board, MyTasks, TeamTasks), **When** it returns, **Then** results are authorization-filtered via subtree Specification before fetch and paginated per contract.

---

### Edge Cases

- What happens when a work item's parent is deleted or archived? Reparenting validation requires same-project membership; root promotion (ParentId = null) is allowed only via `ReparentWorkItem` and is validated for project scope and authorization.
- What happens when a dependency is added between items in different projects? Domain rejects cross-project dependencies unless explicitly allowed by policy — default is intra-project only.
- What happens when an estimate or progress value is negative or exceeds 100%? `Effort` and `ProgressValue` value objects reject out-of-range values at creation and return validation errors.
- What happens when DueDate is in the past at creation? Creation succeeds (overdue indicator will show), but status derivation for `Blocked` does not auto-set status from overdue alone.
- What happens when a work item has no assignee and board is grouped by assignee swimlane? Item appears in an "Unassigned" swimlane.
- What happens when hierarchy depth exceeds expected (e.g., 10 levels via repeated reparenting)? System supports arbitrary depth via same aggregate type; persistence uses recursive CTE and queries remain correct; performance is bounded by indexed ParentId + project filter.
- What happens when a dependency type `RelatedTo` forms a long chain? `RelatedTo` is excluded from cycle detection and never derives `Blocked` status — only `Blocks`/`BlockedBy`/`DependsOn` participate.
- What happens when an assignment request specifies an assignee who is both in subtree and inactive? Active check takes precedence — rejection regardless of subtree membership.
- What happens when the board is queried without a projectId? The query requires projectId — missing parameter returns 400 Bad Request per API contract, not an unfiltered cross-project dump.
- What happens when concurrent dependency additions race to create a cycle? Each addition runs `IDependencyCycleDetector` within the transaction; if the second would close a cycle given the first committed, it is rejected with `CircularDependencyRule` error.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST implement `Project` as an aggregate root (`ProjectId` StronglyTypedId) owning identity, owner, manager, participants (`ProjectMember` with roles), dates, status, priority, criticality, milestones, work items reference, and documents linkage; project metrics wiring (SPEC-004) is consumed as an interface/placeholder and does not block this spec.
- **FR-002**: System MUST raise domain events `ProjectCreated`, `ProjectMemberAdded`, `ProjectMemberRemoved`, `ProjectStatusChanged`, `MilestoneReached` from the `Project` aggregate and publish them via the transactional outbox.
- **FR-003**: Project membership MUST feed Golden Rule A: a project member satisfies the "project membership" branch of hierarchical authorization for assignment and visibility (shared membership with another member grants access when subtree does not).
- **FR-004**: System MUST implement a single `WorkItem` aggregate (`WorkItemId` StronglyTypedId) typed by `WorkItemType` as an `Enumeration` with configurable taxonomy values (e.g., Epic/Feature/Task/Subtask) not hard-coded as classes; adding or reordering taxonomy values MUST NOT require a code change to the aggregate.
- **FR-005**: `WorkItem` MUST persist fields: identity, `ParentId` (nullable FK to same aggregate), `ProjectId`, title, description, `WorkItemType`, `WorkItemStatus`, `WorkItemPriority`, `Criticality`, owner / responsible / reviewer user ids, dates (created, due, started, completed), `ProgressValue`, `Effort` (estimated/actual), `Tag` collection, and `Version` (optimistic concurrency token) per Constitution §Work Item Model.
- **FR-006**: `WorkItem` MUST be created with version 1, `WorkItemCreated` domain event, initial status `Backlog`, and be visible via `GetWorkItemDetail` and `GetKanbanBoard` after commit (outbox pattern).
- **FR-007**: System MUST support arbitrary-depth hierarchy via `ParentId` on the same `WorkItem` aggregate type to implement `Epic → Feature → Task → Subtask` and deeper; depth MUST NOT be hard-coded.
- **FR-008**: Reparenting MUST be performed only via `ReparentWorkItem` domain command that validates: target parent exists and belongs to the same project (or null for root promotion), target is not a descendant of the item (no hierarchy cycle), actor is authorized via SPEC-002 evaluator, and MUST emit `WorkItemReparented`; bare update of `ParentId` via generic update MUST be rejected or absent from the API.
- **FR-009**: System MUST implement `WorkItemStatus` as an `Enumeration` backing an allowed-transition map: `Backlog → Planned → In Progress → Blocked ↔ In Review → Completed` plus reopen rules (`Completed → In Progress` and/or `Completed → Backlog` permitted only per `IWorkItemTransitionPolicy`; configurable, default allows reopen to `In Progress` for managers). Every `from→to` pair MUST be exhaustively testable and non-allowed pairs MUST be rejected.
- **FR-010**: Status transitions MUST be domain operations invoking `CheckRule(new TransitionIsAllowedRule(current, target))`, authorized through SPEC-002 `IAuthorizationEvaluator`, audited (append-only), and raising `WorkItemStatusChanged`; the UI MUST NOT set status directly — drag/drop and any API call MUST issue `ChangeWorkItemStatus` command which is the sole mutation path.
- **FR-011**: System MUST support `WorkItemDependency` aggregate (`WorkItemDependencyId` StronglyTypedId) with fields dependent work item id, principal work item id, and type `Enumeration` (`Blocks`, `BlockedBy`, `DependsOn`, `RelatedTo`); `Blocks`/`BlockedBy` are inverse views of the same relation.
- **FR-012**: Adding a dependency MUST execute `IDependencyCycleDetector` domain service traversing the full transitive closure; if a cycle would be introduced among `Blocks`/`BlockedBy`/`DependsOn` links, `CircularDependencyRule.CheckRule` MUST reject creation with a validation error and no persistence change; `RelatedTo` MUST be excluded from cycle detection and never derive blocked state.
- **FR-013**: Work item `Blocked` status derivation: the domain MUST be able to report whether a work item is logically blocked when unresolved `BlockedBy`/`DependsOn` principals are not in `Completed` (or otherwise resolved); this derivation is informational and does not automatically force the `WorkItemStatus` to `Blocked` without a valid transition.
- **FR-014**: `DependencyRemoved` event MUST be emitted when a dependency is removed via `RemoveDependency` command (authorized and audited).
- **FR-015**: `AssignWorkItem` command MUST validate: assignee user exists and is active, work item is not in `Completed` (or other terminal state per policy), and assignee satisfies authorization — assignee is inside assigner's subtree (via `IManagementHierarchy` Shared Kernel) OR assigner and assignee share membership in the same project. Failure MUST return a domain `Error` and be audited as authorization denial where appropriate.
- **FR-016**: On successful `AssignWorkItem`, system MUST emit `WorkItemAssigned` (and `WorkItemReassigned` on subsequent changes), persist assignment, create an append-only audit entry, and publish a notification integration event for SPEC-008.
- **FR-017**: System MUST provide commands `CreateProject`, `AddProjectMember`, `CreateWorkItem`, `ReparentWorkItem`, `ChangeWorkItemStatus`, `AssignWorkItem`, `AddDependency`, `RemoveDependency`, `CompleteWorkItem` with validation pipeline, `Result`/`Error` handling, and transactional outbox publishing per BuildingBlocks.
- **FR-018**: System MUST provide read-model queries: `GetKanbanBoard(projectId, filters)` (filters: status, assignee, epic, priority, criticality, tags, due-date range; sorting: priority/criticality/dueDate/updatedAt; pagination), `GetWorkItemDetail(id)`, `GetMyTasks(userId)`, `GetTeamTasks(managerId)` (subtree-filtered via `IManagementHierarchy` + project-membership Specification, tenant-aware).
- **FR-019**: Kanban board projection MUST be a read model composed from work items: columns by `WorkItemStatus`, swimlanes by assignee or epic (root Epic ancestor), progress and criticality visualization (`ProgressValue`, `Criticality` badge), overdue indicator (`DueDate < today AND status != Completed`), filters/sorting. The board MUST NEVER mutate state directly — all mutations flow through commands.
- **FR-020**: Optimistic concurrency: every `WorkItem` mutation MUST compare the expected `Version` supplied by the caller with persisted `Version`; on mismatch the operation MUST fail with a concurrency `Error` (mapped to HTTP 409) and MUST NOT silently overwrite; on success `Version` increments by one.
- **FR-021**: Authorization filtering: every query returning protected resources (`GetKanbanBoard`, `GetMyTasks`, `GetTeamTasks`, `GetWorkItemDetail` for authorization-aware callers) MUST compose a subtree/project-membership `Specification<T>` with the resource query before fetching data (never filter after fetching), using SPEC-002 `IAuthorizationEvaluator` and `IManagementHierarchy`.
- **FR-022**: Audit and observability: every security-sensitive or business-significant write (creation, assignment, status transition, dependency change, reparenting, completion) MUST generate an append-only audit entry via the transactional outbox; handlers MUST be idempotent and OpenTelemetry-traced via BuildingBlocks.ServiceDefaults.
- **FR-023**: Domain services `IDependencyCycleDetector`, `IAssignmentPolicy` (who can assign whom — delegates to `IManagementHierarchy` + project membership), and `IWorkItemTransitionPolicy` (including configurable reopen rules) MUST be injectable, testable independently of infrastructure, and be the only place where their respective rules are evaluated.

### Key Entities

- **Project** (Aggregate Root, `ProjectId` StronglyTypedId): Owns identity, owner user id, manager user id, collection of `ProjectMember` (user id + role enumeration), dates, status enumeration, priority, criticality, milestones, and references to work items and documents. Events: `ProjectCreated`, `ProjectMemberAdded`, `ProjectMemberRemoved`, `ProjectStatusChanged`, `MilestoneReached`. Feeds project-membership check for Golden Rule A.
- **WorkItem** (Aggregate Root, `WorkItemId` StronglyTypedId): Single aggregate type for all work granularity. Attributes: id, `ProjectId`, `ParentId` (nullable), `WorkItemType` (Enumeration), `WorkItemStatus` (Enumeration + transition map), `WorkItemPriority`, `Criticality`, owner/responsible/reviewer user ids, dates, `Effort` (estimated/actual), `ProgressValue`, `Tag` collection, `Version` (optimistic concurrency). Events: `WorkItemCreated`, `WorkItemStatusChanged`, `WorkItemAssigned`, `WorkItemReassigned`, `WorkItemReparented`, `WorkItemCompleted`, `ProgressRecalculated`, `WorkItemBlocked`, `DependencyAdded`.
- **WorkItemDependency** (Aggregate Root, `WorkItemDependencyId` StronglyTypedId): Represents a directed dependency between two work items. Attributes: id, dependentId, principalId, type (`Blocks`/`BlockedBy`/`DependsOn`/`RelatedTo` Enumeration). Events: `DependencyAdded` (on WorkItem), `DependencyRemoved`.
- **WorkItemStatus / WorkItemPriority / WorkItemType / Criticality / DependencyType** (Enumeration Value Objects): `Enumeration` subclasses with configured values and — for Status — an allowed-transition map. Types are configurable without code changes to the aggregate.
- **Effort / ProgressValue / DueDate / Tag** (Value Objects): `Effort` (estimated and actual, non-negative), `ProgressValue` (0–100, explainable per Principle XII), `DueDate` (nullable, drives overdue indicator), `Tag` (normalized string). All validated at construction.
- **ProjectMember / Milestone** (Entities within Project): `ProjectMember` — user id, role, joined date; `Milestone` — id, title, due date, reached status.
- **AssignmentScope / HierarchyPath / DependencyGraph** (domain-service concepts): Used by `IAssignmentPolicy` and `IDependencyCycleDetector` to evaluate subtree/membership and cycle invariants.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given a project, when a manager within scope creates a work item, it appears in `GetWorkItemDetail` and `GetKanbanBoard(Backlog)` within one query round-trip, with version 1 and a `WorkItemCreated` event observable in the outbox — verifiable by a single create command followed by two reads.
- **SC-002**: Given a work item in `Backlog`, when `ChangeWorkItemStatus` to `Completed` is issued (no allowed path), the domain rejects with a validation error (e.g., HTTP 400 with `Transition not allowed`), no `WorkItemStatusChanged` is emitted, and the subsequent `GetKanbanBoard` still shows the item in `Backlog`.
- **SC-003**: Given a dependency chain A→B→C (A Blocks B, B Blocks C), when adding dependency C→A, `CircularDependencyRule` rejection occurs in under 200 ms and the persisted dependency graph remains A→B→C with no additional link — verifiable by re-querying dependencies after the rejected command.
- **SC-004**: Given two concurrent updates to the same work item both starting from version N, when both save, exactly one succeeds (version N+1) and the other receives a concurrency `Error` (HTTP 409), never a silent overwrite — verifiable in under 1 second in a 2-request race test.
- **SC-005**: Given a subtree-member assignment request from outside the subtree without project membership, when `AssignWorkItem` executes, it is denied (generic denial to caller), no `WorkItemAssigned` is emitted, and an audited authorization-denied entry exists — verifiable by querying the audit store after the denied call.
- **SC-006**: Given any status or assignment change, when committed, an append-only audit event (actor, action, resource type/id, before/after, tenant, correlation ID) and, where relevant, a notification integration event are durably present in the outbox within the same transaction — verifiable by polling the outbox after the command.
- **SC-007**: Board query correctness: seeding a project with 50 work items across statuses/assignees/epics, `GetKanbanBoard(projectId)` returns all items grouped into correct status columns in under 500 ms, and filtered queries (`status=In Progress`, `assignee=A1`) return only matching, authorization-filtered items with correct pagination metadata.
- **SC-008**: Kanban drag/drop E2E round-trip: board query → `ChangeWorkItemStatus` (valid) → board re-query completes and the item appears in its new column within 1 second end-to-end, verified by automated E2E test that uses the actual API chain (not mocked projection).
- **SC-009**: 100% of defined `WorkItemStatus` `from→to` pairs are exercised by unit tests; any non-allowed transition not covered by the transition map is rejected and the test suite passes exhaustively.

## Assumptions

- SPEC-002 `IManagementHierarchy` (IsInSubtree, GetSubtree, GetAncestors, GetCommonAncestor) and `IAuthorizationEvaluator` (Golden Rule A) are available as Shared Kernel / domain service contracts; this spec consumes them via interfaces and does not re-implement hierarchy storage. Until SPEC-002 is complete, tests may use an in-memory stub per draft `buildingblocks.md` `Specification<T>` patterns.
- `oroidentityserver` OIDC identity and `tenant_id` from SPEC-002 are assumed configured; every command/query carries authenticated actor identity and tenant context.
- `WorkItemType` taxonomy values are configurable via `Enumeration` rows (e.g., seed Epic/Feature/Task/Subtask) and default ordering is Epic > Feature > Task > Subtask for display, but hierarchy depth is not enforced to match that ordering — any `WorkItemType` may parent any other (validated by policy, not by type hierarchy) unless a future policy tightens it.
- Reopen rules: default allows `Completed → In Progress` for users with `workitem.update` permission who are either in the manager subtree or a project member; `Completed → Backlog` requires manager role. Behavior is configurable via `IWorkItemTransitionPolicy`.
- Cross-project dependencies are disallowed by default; a future configuration may allow inter-project `RelatedTo` only, never `Blocks`/`DependsOn` across projects.
- `WorkItemStatus` transition map shipped initially: `Backlog → Planned`, `Planned → In Progress`, `In Progress → Blocked`, `In Progress → In Review`, `Blocked ↔ In Review`, `In Review → Completed`, plus the reopen rules above. Any path not listed is invalid (e.g., `Backlog → Completed`, `Backlog → In Progress`).
- `ProgressValue` calculation is manual or aggregable from subtask completion (weighted) per Principle XII, but its full explainability wiring (SPEC-004 Metrics) is out of scope — this spec only stores and visualizes the value, not derives it from metrics.
- `Tag` normalization: trimmed, lowercased, 1–50 characters, alphanumeric plus hyphen/underscore, duplicate tags deduped at write time.
- `Effort` values are non-negative decimals in hours with one decimal precision; actual effort may exceed estimated without error, but negative values are rejected.
- Notifications for `WorkItemAssigned`/`WorkItemStatusChanged` are published as integration events consumed by SPEC-008; this spec only emits the integration event via outbox, it does not implement the notification delivery itself.
- Optimistic concurrency uses an integer `Version` starting at 1 and incrementing on each successful mutation; API returns version in ETag and response body, and expects `If-Match` or body `expectedVersion` on writes.
- Overdue indicator logic: `DueDate != null AND DueDate < Today (UTC) AND Status != Completed`; visualization (badge color/icon) is specified by the design system skill but content is driven by this rule.

## Dependencies & Traceability

- **Depends on**: SPEC-002 Identity, Access and Organization — `IManagementHierarchy`, `IAuthorizationEvaluator`, Golden Rule A (tenant + subtree + project membership + ownership + classification), audit outbox pattern.
- **Enables**: SPEC-004 Metrics & Progress wiring; SPEC-008 Notifications (consumes `WorkItemAssigned`/`WorkItemStatusChanged`); SPEC-013 E2E Kanban drag/drop chain.
- **Constitution**: Principles VI (domain rules via CheckRule/Specification), VII-VIII (hierarchical auth + auditable), XIV (state transitions controlled), XVI (APIs are contracts), XII-XIII (progress explainable, metrics configurable), V (modular BC-03), XX-XXI (testability, TDD+DDD+Vertical Slices, BuildingBlocks canon). §Work Item Model.

## Out of Scope

- Detailed metrics formulas and progress auto-recalculation beyond storing/visualizing `ProgressValue` (SPEC-004).
- Full document lifecycle linked to work items beyond document reference fields (BC-06 Documents).
- Real-time collaborative editing or WebSocket push for board updates — board refresh is poll/query-based; real-time may be added later.
- Search/indexing across work items (BC Search) — board queries are EF-backed with Specification filtering, not search-index backed.
- AI/LLM features for work items (BC-08).

