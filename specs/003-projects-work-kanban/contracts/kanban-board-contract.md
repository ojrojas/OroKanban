# Contract: Kanban Board Read Model

**Module**: `Projects` (BC-03) — **Projection**: read model, never mutates state | **Query**: `GetKanbanBoardQuery : IQuery<Result<KanbanBoardResponse>>` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Source**: EF `IQueryable<WorkItem>` projection (not search index)

## GET /api/projects/{projectId}/board — GetKanbanBoard

### Query params

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `projectId` | guid (route) | yes | Missing → `400 Bad Request` (never unfiltered cross-project dump) |
| `status` | `string` csv | no | Filter: `Backlog,Planned,...` — only items with matching `WorkItemStatus` |
| `assignee` | `guid` csv | no | Filter by `responsibleId` |
| `epic` | `guid` | no | Filter by root Epic ancestor (resolved via recursive CTE ancestry) |
| `priority` | `string` csv | no | `Low..Urgent` |
| `criticality` | `string` csv | no | `Low..Critical` |
| `tags` | `string` csv | no | `and` semantics — item must have all tags (normalized) |
| `dueFrom` / `dueTo` | `ISO8601 date` | no | Inclusive `DueDate` range filter |
| `search` | `string` | no | Substring on `title`/`description` (`ilike` via Specification) |
| `swimlane` | `string` | no | `assignee` (group by `responsibleId`, unassigned → `Unassigned` lane) \| `epic` (group by root Epic id via ancestry CTE) \| omitted = no swimlanes (flat columns) |
| `sortBy` | `string` | no | `priority` \| `criticality` \| `dueDate` \| `updatedAt` \| `createdAt` (default `updatedAt desc`) |
| `sortDir` | `string` | no | `asc` \| `desc` |
| `page` | `int 1-based` | no | Default 1 |
| `pageSize` | `int` | no | Default 20, max 100 |

All filters (including `search`) are translated to a `Specification<WorkItem>` and composed via `And` before any `ListAsync` — authorization `Specification` is composed first (see Authorization).

### Response 200 OK

```json
{
  "projectId": "guid",
  "generatedAt": "2026-09-01T12:00:00Z",
  "columns": [
    {
      "status": "Backlog",
      "statusId": 1,
      "count": 12,
      "items": [
        {
          "id": "guid",
          "title": "Implement due-date indicator",
          "type": "Task",
          "status": "Backlog",
          "priority": "High",
          "criticality": "Medium",
          "responsibleId": "guid | null",
          "responsibleName": "string | null (joined from identity projection, optional)",
          "dueDate": "2026-10-01T00:00:00Z | null",
          "isOverdue": true,
          "progress": 25,
          "tags": ["kanban","ui"],
          "parentId": "guid | null",
          "epicId": "guid | null (root Epic via ancestry)",
          "blockedDerived": false,
          "version": 3,
          "updatedAt": "2026-09-01T10:00:00Z"
        }
      ]
    },
    { "status": "Planned", "statusId": 2, "count": 4, "items": [...] },
    { "status": "InProgress", "statusId": 3, "count": 7, "items": [...] },
    { "status": "Blocked", "statusId": 4, "count": 2, "items": [...] },
    { "status": "InReview", "statusId": 5, "count": 3, "items": [...] },
    { "status": "Completed", "statusId": 6, "count": 9, "items": [...] }
  ],
  "swimlanes": [
    {
      "key": "guid | Unassigned | epic:guid",
      "label": "Alice Johnson | Unassigned | Epic: Revamp checkout epic",
      "columns": [ { "status": "Backlog", "count": 2, "items": [...] }, ... ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 37,
  "filtersApplied": { "status": ["InProgress"], "assignee": ["guid"], "swimlane": "assignee" },
  "overdueCount": 3
}
```

### Authorization (before fetch, not post-filter)

`GetKanbanBoardQueryHandler`:

1. Extracts `actorId` + `tenantId` from `TenantContext`/`ClaimsPrincipal`.
2. Resolves `allowedUserIds = await hierarchy.GetSubtree(actorId)` ∪ `projectMembership`∪`ownership`∪`explicit grants` via `IAuthorizationEvaluator` / `IAssignmentPolicy` helpers (tenant is first gate — tenant mismatch → empty set, `200 []` not 403).
3. Composes `AuthorizedWorkItemSpec = new TenantSpec(tenantId).And(new AuthorizedActorSpec(allowedUserIds, actorId)).And(projectFilter).And(user-supplied filters)` as a `Specification<WorkItem>` and passes it to `repository.ListAsync(spec, ct)` so **SQL `WHERE` does the filtering**. Never `ListAsync(all).Where(auth)` (which would leak before filtering).
4. Cross-branch caller without membership/grant sees `200 { columns: [ { status, count:0, items:[] } ...], totalCount:0 }` — never an error that reveals existence.

### Projection rules

| Rule | Detail |
|------|--------|
| **Never mutates** | No `POST`/`PUT`/`PATCH`/`DELETE` on `/board`; board endpoints are `IQuery` only. Drag/drop in Web calls `POST /api/workitems/{id}/status` (workitems contract), then re-queries `/board`. |
| **Columns** | Exactly the `WorkItemStatus` enumeration values, ordered `Backlog→...→Completed` (seed order), even when empty. |
| **Swimlanes — assignee** | Group by `responsibleId`; items with `null` → single `Unassigned` lane (first swimlane). |
| **Swimlanes — epic** | Group by root ancestor where `type == Epic` via `IHierarchyInspector.GetRootEpicId` (CTE); items not under an Epic → `NoEpic` lane. |
| **Sorting within column** | Stable sort per `sortBy`/`sortDir`; default `updatedAt desc`. |
| **Progress** | `ProgressValue.Percent` → `progressProgress bar` in Web (no server derivation beyond stored VO). |
| **Criticality** | `Criticality` Enumeration → badge color mapping lives in `minimal-ui-design-system` tokens; server returns the value, Web maps to color. |
| **Overdue** | Per-item `isOverdue = dueDate != null && dueDate < DateTime.UtcNow && status != Completed`. Board aggregates `overdueCount`. |
| **BlockedDerived** | Per-item `blockedDerived = exists Dep WHERE dependent==id AND principal.Status != Completed AND type in (Blocks,BlockedBy,DependsOn)`. Requires no extra query if handler eager-loads `dependencies` filtered. |

### Performance

`GetKanbanBoard(projectId)` with 50 items <500 ms p95 (SC-007); board query is a single round-trip `IQueryable` with `Include` only for tags/dependencies; pagination controls wire-level size (column counts reflect filtered totals, not page-window totals; `totalCount` is `CountAsync(spec)`).

### Errors

`400 Bad Request` (`projectId` missing or invalid enum in `status` filter), `401`, `403` generic (no leak), never `404` for board on unknown project inside actor's tenant vs. wrong tenant — same `404` shape to avoid tenant existence leak. `Result<PagedResult>` mapping per workitems contract.

### Frontend contract notes (Web)

- Web `src/app/features/kanban/` consumes this contract via `GET /api/projects/{projectId}/board?page&pageSize&swimlane&...`.
- State managed with `ngrx-signal-store` (`boardStore` with `withState({ columns, swimlanes, filters, sort, page })`, `withMethods({ loadBoard, setFilter, dragDrop })`; `dragDrop` dispatches `ChangeWorkItemStatusCommand` then `loadBoard`).
- Design tokens/elevation for board cards/columns from `minimal-ui-design-system` skill (no contract change needed for styling).
