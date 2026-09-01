# Data Model: Projects, Work Items and Kanban

**Feature**: 003-projects-work-kanban | **Date**: 2026-09-01 | **Schema**: `projects` (`ProjectsDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("projects")` + `ApplyConfiguration(new OutboxEntityTypeConfiguration())`)

## Entities

### 1. Project (AggregateRoot, BC-03, `projects.projects`)

Published by `Projects.Domain`, persisted via `ProjectsDbContext` (outbox + RowVersion per constitution).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `ProjectId : StronglyTypedId<Guid>` | PK, `Guid.NewGuid()` on `CreateProject` | Root identifier |
| `TenantId` | `Guid` | required, from `TenantContext` (JWT `tenant_id`) | Tenant isolation — all queries include it |
| `Name` | `string` | required, 3–200 chars, unique per tenant (filtered index) | Identity |
| `Description` | `string?` | max 4k | |
| `OwnerId` | `Guid` (`UserId` underlying) | FK to OroIdentityServer user | Golden Rule A ownership |
| `ManagerId` | `Guid` | FK to user, must be assigner at creation | Manager for board visibility |
| `Status` | `ProjectStatus : Enumeration` | `Draft/Active/OnHold/Completed/Archived` | Seeded enumeration |
| `Priority` | `ProjectPriority : Enumeration` | `Low/Medium/High/Critical` | Seeded |
| `Criticality` | `Criticality : Enumeration` | `Low/Medium/High/Critical` | Same VO as WorkItem |
| `StartDate` | `DateTime?` | `<= DueDate` when both set | |
| `DueDate` | `DateTime?` | nullable | |
| `RowVersion` | `byte[]` | `IsRowVersion()` / `IsConcurrencyToken()` | Optimistic concurrency |
| `CreatedAt` / `UpdatedAt` | `DateTime` | set by `AppDbContextBase` | |

**Children (owned entities / tables)**:

| Child | Table | Fields | Constraints |
|-------|-------|--------|-------------|
| `ProjectMember` | `projects.project_members` | `Id: Guid` (surrogate PK), `ProjectId: ProjectId` (FK), `UserId: Guid`, `Role: ProjectRole : Enumeration` (`Owner/Manager/Contributor/Reviewer`), `JoinedAt: DateTime` | Unique `(ProjectId, UserId)`; member feeds `IProjectMembership.IsMember` |
| `Milestone` | `projects.project_milestones` | `Id: Guid`, `ProjectId`, `Title: string` (1–200), `DueDate: DateTime?`, `IsReached: bool`, `ReachedAt: DateTime?` | `Title` unique per project |

**Events (domain → outbox → integration)**: `ProjectCreated {ProjectId, TenantId}`, `ProjectMemberAdded {ProjectId, UserId, Role}`, `ProjectMemberRemoved {ProjectId, UserId}`, `ProjectStatusChanged {ProjectId, From, To}`, `MilestoneReached {ProjectId, MilestoneId}`.

### 2. WorkItem (AggregateRoot, BC-03, `projects.work_items`)

Single aggregate type for all granularities; taxonomy comes from `WorkItemType` Enumeration, not subclasses.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `WorkItemId : StronglyTypedId<Guid>` | PK | |
| `TenantId` | `Guid` | required | Tenant isolation — part of every `Specification<T>` |
| `ProjectId` | `ProjectId` | FK `projects.projects`, required, indexed | Scope (all dependencies/assignment checked within project) |
| `ParentId` | `WorkItemId?` | nullable self-FK `work_items.id`, indexed with `project_id` | Arbitrary depth (research Decision 1); null = root |
| `Title` | `string` | required, 1–200, trimmed | |
| `Description` | `string?` | max 10k | |
| `Type` | `WorkItemType : Enumeration` | `Epic(1)/Feature(2)/Task(3)/Subtask(4)` seeded, extensible | Configurable taxonomy — adding a value is a seed row, no aggregate change |
| `Status` | `WorkItemStatus : Enumeration` | `Backlog(1)/Planned(2)/InProgress(3)/Blocked(4)/InReview(5)/Completed(6)` | Transition map via `IWorkItemTransitionPolicy` (research Decision 2) |
| `Priority` | `WorkItemPriority : Enumeration` | `Low/Medium/High/Critical/Urgent` seeded | |
| `Criticality` | `Criticality : Enumeration` | `Low/Medium/High/Critical` | For board badge |
| `OwnerId` | `Guid?` | nullable FK user | Owner/creator |
| `ResponsibleId` | `Guid?` | nullable FK user (current assignee) | Assignment target — `AssignWorkItem` sets this |
| `ReviewerId` | `Guid?` | nullable FK user | |
| `StartDate` | `DateTime?` | `<= DueDate` when both set | |
| `DueDate` | `DueDate : ValueObject` (wraps `DateTime?`) | nullable, drives `OverdueIndicator` | `IsOverdue(now) => Value != null && Value < now && Status != Completed` |
| `CompletedAt` | `DateTime?` | set when `CompleteWorkItem` or `Status→Completed` | |
| `Progress` | `ProgressValue : ValueObject` | 0–100, validated | `ProgressRecalculated` explains inputs (Principle XII) |
| `EstimatedEffort` | `Effort : ValueObject` | `Hours: decimal >=0, precision 0.1` | Non-negative |
| `ActualEffort` | `Effort : ValueObject` | `>=0`, may exceed estimate | |
| `Tags` | `IReadOnlyCollection<Tag>` | owned collection → `jsonb` or `projects.work_item_tags` join (Tag VO: trimmed, lowercased, 1–50, `[a-z0-9_-]+`, deduped) | Specification-filterable |
| `Version` | `int` | starts 1, `Version++` on each domain mutation | Exposed int + underlying `RowVersion byte[]` token (research Decision 5) |
| `RowVersion` | `byte[]` | `IsRowVersion()` concurrency token | EF checks on `SaveChanges` → 409 on mismatch |
| `CreatedAt` / `UpdatedAt` / `CreatedBy` | `DateTime` / `Guid` | audit fields via `AppDbContextBase` | |

**Invariants**: `ParentId != Id`; `ParentId` descendant check via `ReparentNoCycleRule` (recursive CTE); cross-project reparent/dependency rejected unless `RelatedTo`-only policy allows; `Type` must be a seeded `Enumeration` value; `Tags`/`Effort`/`Progress`/`DueDate` validated at VO construction; `Version` increments atomically with RowVersion.

**Events**: `WorkItemCreated {WorkItemId, ProjectId, Type, Status=Backlog, Version=1}`, `WorkItemStatusChanged {WorkItemId, From, To, Actor}`, `WorkItemAssigned {WorkItemId, AssigneeId, FromResponsible}`, `WorkItemReassigned` (successive assign), `WorkItemReparented {WorkItemId, OldParentId, NewParentId}`, `WorkItemCompleted {WorkItemId, CompletedAt}`, `ProgressRecalculated {WorkItemId, Old, New, Inputs}`, `WorkItemBlocked {WorkItemId, BlockedByIds}`, `DependencyAdded {DependencyId, DependentId, PrincipalId, Type}`.

**State transitions** (via `WorkItem.ChangeStatus(target, policy)` + `CheckRule(TransitionIsAllowedRule)`):

```
Backlog → Planned
Planned → InProgress
InProgress → Blocked | InReview
Blocked ↔ InReview
InReview → Completed
Completed → InProgress (reopen, per IWorkItemTransitionPolicy; Completed → Backlog manager-only variant)
Any pair not listed → Error.Validation("Transition not allowed") — 36-pair exhaustive coverage
```

### 3. WorkItemDependency (AggregateRoot, `projects.work_item_dependencies`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `WorkItemDependencyId : StronglyTypedId<Guid>` | PK | |
| `TenantId` | `Guid` | required | Tenant-scoped |
| `DependentId` | `WorkItemId` | FK `work_items.id`, indexed | "A depends on / is blocked by" |
| `PrincipalId` | `WorkItemId` | FK `work_items.id`, indexed | Blocking item |
| `Type` | `DependencyType : Enumeration` | `Blocks(1)/BlockedBy(2)/DependsOn(3)/RelatedTo(4)` | `BlockedBy` is inverse view of `Blocks`; `RelatedTo` excluded from cycle/blocked logic |

**Constraints**: `DependentId != PrincipalId`; unique `(DependentId, PrincipalId)` prevents duplicate edges; `DependentId` and `PrincipalId` must be in same `project_id` (and same `tenant_id`) unless `type==RelatedTo` and cross-project policy is enabled (default deny); `CheckRule(CircularDependencyRule)` via `IDependencyCycleDetector` before insert (research Decision 3).

**Blocked derivation** (informational, not auto-status): `IsBlocked(workItemId) => exists unresolved non-RelatedTo edge where dependent==workItemId AND principal.Status != Completed`.

**Events**: `DependencyAdded` (on WorkItem aggregate) + `DependencyRemoved {DependencyId}` on removal (authorized+audited).

### 4. Enumerations (seeded, not hard-coded, per-module tables or `projects.enumerations`)

| Enumeration | Values (seed) | Notes |
|-------------|---------------|-------|
| `WorkItemType` | `Epic(1), Feature(2), Task(3), Subtask(4)` | Extensible without aggregate change |
| `WorkItemStatus` | `Backlog(1)..Completed(6)` + map in `IWorkItemTransitionPolicy` | `Enumeration` per BuildingBlocks |
| `WorkItemPriority` | `Low, Medium, High, Critical, Urgent` | |
| `Criticality` | `Low, Medium, High, Critical` | Shared with Project |
| `DependencyType` | `Blocks, BlockedBy, DependsOn, RelatedTo` | |
| `ProjectStatus` | `Draft, Active, OnHold, Completed, Archived` | |
| `ProjectRole` | `Owner, Manager, Contributor, Reviewer` | For `ProjectMember` |
| `ProjectPriority` | `Low, Medium, High, Critical` | |

Seeding via `Projects.Infrastructure/Seed/ProjectsEnumerationSeederHostedService` on first run (or EF `HasData`).

### 5. Value Objects

| VO | Fields | Validation | Purpose |
|----|--------|------------|---------|
| `Effort` | `Hours: decimal` | `>=0, precision 0.1, max 9999.9` | Estimated/actual hours |
| `ProgressValue` | `Percent: int` | `0..100` | Explainable progress (SPEC-004 wires inputs) |
| `DueDate` | `Value: DateTime?` | null or UTC date | `IsOverdue(now)` rule for board indicator |
| `Tag` | `Value: string` | `1..50, trimmed/lowercased, ^[a-z0-9_-]+$`, deduped | Filter/sort on board |

### 6. Domain Services (injectable, testable without infrastructure)

| Service | Contract | Published By | Implemented By |
|---------|----------|--------------|----------------|
| `IDependencyCycleDetector` | `HasCycle(allEdges, candidateEdge) → bool` (`HasCycleAsync(projectId, candidate) → bool` variant) | `Projects.Domain` | `Projects.Infrastructure` (in-memory DFS, `RelatedTo` filtered; see research Decision 3) |
| `IAssignmentPolicy` | `CanAssign(assignerId, assigneeId, projectId, tenantId, ct) → Result` + `IsInSameProjectOrSubtree(...)` | `Projects.Domain` | `Projects.Infrastructure` (delegates to `IManagementHierarchy` + `IProjectMembership` + `IUserStateChecker`; see research Decision 4) |
| `IWorkItemTransitionPolicy` | `IsAllowed(from, to, actorRoles) → bool`, `AllowedFrom(from) → IReadOnlySet<Status>` | `Projects.Domain` | `Projects.Infrastructure` (dictionary map + reopen options; research Decision 2) |
| `IHierarchyInspector` | `GetAncestorIds(workItemId) / GetDescendantIds(workItemId) / GetRootEpicId(workItemId)` | `Projects.Domain` (internal) | `Projects.Infrastructure` (recursive CTE over `work_items`; research Decision 1) |
| `IManagementHierarchy` | `IsInSubtree(managerId, userId), GetSubtree, GetAncestors, GetCommonAncestor` | `Organization.Contracts` (Shared Kernel, consumed) | `Organization.Infrastructure` (already implemented — reused) |
| `IProjectMembership` | `IsMember(userId, projectId) / GetProjectIdsForUser(userId)` | `Projects.Contracts` (published) / consumed by `Organization.Infrastructure` | `Projects.Infrastructure` (simple `Exists` query over `project_members`) |

### Relationships Overview

```
Project (1) ──< ProjectMember (many, unique ProjectId+UserId) ──┐
     │  1                                                      │ feeds IProjectMembership
     │                                                        │
     └─────────< Milestone                                     │
                                                               │
Project (1) ──< WorkItem (many, via ProjectId) ──< WorkItemDependency (many, via DependentId/PrincipalId)
     │              │ (self-FK ParentId, arbitrary depth)      │
     │              └── Tags / Effort / Progress / DueDate      │
     │              └── Version + RowVersion (concurrency)      │
     │                                                        │
     WorkItem ── uses ── WorkItemStatus/Type/Priority/Criticality (Enumeration, seeded)
     WorkItem ── checked by ── TransitionIsAllowedRule / ReparentNoCycleRule / CircularDependencyRule / WorkItemNotCompletedRule
     IAssignmentPolicy ── uses ── IManagementHierarchy + IProjectMembership ── gate for AssignWorkItem
     GetKanbanBoard ── composes ── AuthorizedWorkItemSpec (tenant + subtree/project-membership) before fetch
     All writes ── outbox ── audit.append-only + notification integration events (→ Notifications BC)
```

### Validation Rules (from spec → model)

- FR-001/002: `Project` invariants + `ProjectCreated/MemberAdded` via `AppDbContextBase` outbox; member uniqueness enforced by unique index.
- FR-004/005: `WorkItemType` must be seeded enumeration; `Title`/`Version`/`Tags`/`Effort`/`Progress`/`DueDate` validated at VO/aggregate construction; `Version` starts 1 and increments with RowVersion.
- FR-007/008: `ParentId` self-FK; `ReparentWorkItem` validates same project, not descendant (CTE), authorized, audited, emits `WorkItemReparented`; bare `ParentId` update absent from API.
- FR-009/010: `WorkItemStatus` transition map + `TransitionIsAllowedRule.CheckRule`; UI never sets status — only `ChangeWorkItemStatus`.
- FR-011/012: `WorkItemDependency` unique `(DependentId, PrincipalId)`, `CircularDependencyRule` via `IDependencyCycleDetector` (`RelatedTo` excluded).
- FR-015/016: `AssignWorkItem` validates active, not Completed, subtree-or-shared-membership via `IAssignmentPolicy` + evaluator `workitem.assign` permission.
- FR-020: RowVersion + `Version` check → `DbUpdateConcurrencyException` → `Error.Conflict` → 409, never 5xx.
- FR-021/022: Board/task queries compose authorization `Specification<T>` before `ListAsync`; all writes produce audit via same outbox transaction.
