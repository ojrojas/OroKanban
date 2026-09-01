# Contract: Work Items API

**Module**: `Projects` (BC-03) | **Base path**: `/api/projects/{projectId}/workitems` + `/api/workitems/{id}` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result<T>→HTTP`, manual mapping, `IEndpoint` per slice, pagination/filtering/sorting envelopes per Principle XVI.

## POST /api/projects/{projectId}/workitems — CreateWorkItem

**Command**: `CreateWorkItemCommand : ICommand<Result<CreateWorkItemResponse>>`

```json
// Request
{
  "title": "Implement due-date indicator",
  "description": "Add overdue badge to board cards",
  "type": "Task",
  "priority": "High",
  "criticality": "Medium",
  "parentId": "guid | null (must be in same project, not cause cycle)",
  "ownerId": "guid | null",
  "responsibleId": "guid | null (initial assignee; still validated via IAssignmentPolicy if set)",
  "reviewerId": "guid | null",
  "dueDate": "2026-10-01T00:00:00Z | null",
  "estimatedEffortHours": 8.0,
  "tags": ["kanban", "ui"],
  "progress": 0
}
// Response 201 Created — Location: /api/workitems/{id}
{
  "id": "guid (WorkItemId)",
  "projectId": "guid",
  "parentId": "guid | null",
  "title": "Implement due-date indicator",
  "type": "Task",
  "status": "Backlog",
  "priority": "High",
  "criticality": "Medium",
  "responsibleId": "guid | null",
  "dueDate": "2026-10-01T00:00:00Z | null",
  "progress": 0,
  "tags": ["kanban","ui"],
  "version": 1,
  "createdAt": "2026-09-01T...",
  "isOverdue": false
}
// Errors: 400 Validation (title required 1–200, type not in Enumeration, parent not in same project, tags/effort/progress invalid), 403 denied (evaluator denies workitem.create), 404 parent not found
```

Domain: `WorkItem.Create(...)` with `Version=1`, `Status=Backlog`, VO-validated fields → `WorkItemCreated` → outbox `WorkItemCreatedIntegrationEvent`. Visible via `GetWorkItemDetail` and `GetKanbanBoard(Backlog)`.

## GET /api/workitems/{id} — GetWorkItemDetail

**Query**: `GetWorkItemDetailQuery(id) : IQuery<Result<WorkItemDetailResponse>>`

```json
{
  "id": "guid", "projectId": "guid", "parentId": "guid | null",
  "title": "...", "description": "...", "type": "Task", "status": "InProgress",
  "priority": "High", "criticality": "Medium",
  "ownerId": "guid | null", "responsibleId": "guid | null", "reviewerId": "guid | null",
  "dueDate": "2026-10-01T...", "startDate": null, "completedAt": null,
  "estimatedEffortHours": 8.0, "actualEffortHours": 2.5,
  "progress": 42, "isOverdue": true,
  "tags": ["kanban"], "ancestors": ["guid: Epic .../Feature ancestry via ParentId"],
  "dependencies": [{ "dependencyId": "guid", "principalId": "guid", "type": "Blocks", "principalStatus": "InProgress" }],
  "blockedDerived": true,
  "version": 3, "updatedAt": "...", "tenantId": "guid"
}
```

**Authorization**: `AuthorizedWorkItemSpec(actorId, tenantId)` composed before fetch (see kanban contract); cross-branch without membership returns `404`.

## POST /api/workitems/{id}/reparent — ReparentWorkItem

**Command**: `ReparentWorkItemCommand(workItemId, newParentId, expectedVersion) : ICommand<Result<WorkItemDetailResponse>>`

```json
// POST /api/workitems/{id}/reparent  — only way to change ParentId (bare ParentId update not exposed)
{
  "newParentId": "guid | null (null = promote to root)",
  "expectedVersion": 3
}
// Response 200 OK — updated detail with new ParentId and incremented Version
// Errors: 400 (same-project check, not-descendant CTE check, cycle), 403 denied, 404 parent not found, 409 Conflict (Version mismatch)
```

Raises `WorkItemReparented {WorkItemId, OldParentId, NewParentId}`. Audited.

## POST /api/workitems/{id}/status — ChangeWorkItemStatus

**Command**: `ChangeWorkItemStatusCommand(workItemId, targetStatus, expectedVersion) : ICommand<Result<WorkItemDetailResponse>>`

```json
// POST /api/workitems/{id}/status  — ONLY mutation path (UI drag/drop calls this; UI never PUTs status field)
{
  "targetStatus": "Planned",
  "expectedVersion": 2
}
// Response 200 OK — WorkItemDetailResponse with new status + Version+1
// Errors: 400 Validation ("Transition not allowed: Backlog → Completed"), 403 denied (evaluator denies workitem.update or transition not authorized for role), 404 not found, 409 Conflict
```

**Domain**: `workItem.ChangeStatus(targetStatus, transitionPolicy)` → `CheckRule(new TransitionIsAllowedRule(current, target))` → evaluator → audit → `WorkItemStatusChanged {WorkItemId, From, To, Actor}` + `WorkItemBlocked`/`WorkItemCompleted`/`MilestoneReached` where applicable. Allowed map:

```
Backlog → Planned
Planned → InProgress
InProgress → Blocked | InReview
Blocked ↔ InReview
InReview → Completed
Completed → InProgress (reopen, per IWorkItemTransitionPolicy; Completed→Backlog manager-only variant)
```

## POST /api/workitems/{id}/assign — AssignWorkItem

**Command**: `AssignWorkItemCommand(workItemId, assigneeId, expectedVersion) : ICommand<Result<WorkItemDetailResponse>>`

```json
// POST /api/workitems/{id}/assign
{
  "assigneeId": "guid",
  "expectedVersion": 4
}
// Response 200 OK — detail with updated responsibleId + Version+1
// Errors: 400 (assignee inactive, work item Completed, assignee not found), 403 denied (not in subtree AND no shared project membership — deny reason internal, caller sees generic Forbidden + audit authorization.denied), 404 work item not found, 409 Conflict
```

Validated via `IAssignmentPolicy` (`IManagementHierarchy.IsInSubtree` OR shared `IProjectMembership.IsMember` for both assigner+assignee, plus `IUserStateChecker.IsActive`). Raises `WorkItemAssigned` (first) / `WorkItemReassigned` (subsequent) → outbox `WorkItemAssignedIntegrationEvent` → Notifications (SPEC-008). Audited.

## POST /api/workitems/{id}/dependencies — AddDependency

**Command**: `AddDependencyCommand(dependentId, principalId, type, expectedVersion?) : ICommand<Result<DependencyResponse>>`

```json
// POST /api/workitems/{id}/dependencies
{
  "principalId": "guid",
  "type": "Blocks"
}
// Response 201 — { "dependencyId": "guid", "dependentId": "guid", "principalId": "guid", "type": "Blocks" }
// Errors: 400 Validation (Dependent==Principal, cross-project unless RelatedTo with cross-project policy, CircularDependencyRule), 403 denied, 404 principal/dependent not found, 409 Conflict
```

Cycle detection: `IDependencyCycleDetector.HasCycle(allNonRelatedToEdges + candidate)` → `CircularDependencyRule` → `Error.Validation("Circular dependency")`. `RelatedTo` never participates. Raises `DependencyAdded` on the WorkItem aggregate.

## DELETE /api/workitems/dependencies/{dependencyId} — RemoveDependency

**Command**: `RemoveDependencyCommand(dependencyId, expectedVersion?) : ICommand<Result>`

Raises `DependencyRemoved`. Authorized + audited.

## POST /api/workitems/{id}/complete — CompleteWorkItem

**Command**: `CompleteWorkItemCommand(workItemId, expectedVersion, actualEffortHours?) : ICommand<Result<WorkItemDetailResponse>>`

Convenience that issues `ChangeWorkItemStatus(target=Completed, ...)` after verifying `InReview→Completed` and sets `CompletedAt`/`ActualEffort`. Exists as explicit business intent ("complete" vs "any transition"); also raises `WorkItemCompleted` and triggers `ProgressRecalculated` on ancestors.

## GET /api/users/me/tasks — GetMyTasks

**Query**: `GetMyTasksQuery(userId, filters) : IQuery<Result<PagedResult<WorkItemSummary>>>`

Returns items where `responsibleId == me` (plus optionally `ownerId == me`), scoped by actor's tenant, with pagination/sorting/filters (status, projectId, dueDateRange, criticality, tags). Authorization via `AuthorizedWorkItemSpec` so cross-tenant items are never returned.

## GET /api/managers/{managerId}/tasks — GetTeamTasks

**Query**: `GetTeamTasksQuery(managerId, filters) : IQuery<Result<PagedResult<WorkItemSummary>>>`

Subtree-filtered: resolves `IManagementHierarchy.GetSubtree(managerId)` then `WorkItem where responsibleId in subtree OR via project membership Specification`. Only allowed if actor is managerId or ancestor of managerId (via evaluator); otherwise empty set (not a 403 that leaks existence).

## Concurrency envelope

All writes carry concurrency:

- Request: `expectedVersion: int` in body OR `If-Match: "<version>"` header.
- Response: `Version` in body; optional `ETag: W/"<version>"` header.
- Mismatch: `Error.Conflict("Concurrency conflict")` → **HTTP 409** with body `{ code: "conflict", message: "Concurrency conflict", currentVersion: N }`. Never silent overwrite, never 5xx.

## Error envelope (all endpoints)

```json
// 400 Validation, 403 Forbidden (generic), 404 Not Found, 409 Conflict share envelope:
{
  "type": "https://orokanban.local/errors/{code}",
  "title": "Validation failed | Forbidden | Not Found | Concurrency conflict",
  "status": 400|403|404|409,
  "detail": "Human-readable (deny reason NOT included in 403 body; audited server-side)",
  "errors": { "Field": ["message"] },
  "traceId": "00-..."
}
```
`Result<T>` → HTTP via `ToHttpResult()`; `Error.Validation→400`, `Error.Forbidden→403 generic`, `Error.NotFound→404`, `Error.Conflict→409`.
