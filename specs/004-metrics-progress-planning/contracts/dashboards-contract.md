# Contract: Dashboards API

**Module**: `Metrics` (BC-04) | **Base path**: `/api/dashboards` or `/api/metrics/health` | **Auth**: Bearer JWT (`tenant_id`) | **Golden Rule A**: every aggregation is `IManagementHierarchy` subtree/membership **before** fetch, never post-filtered.

## GET /api/metrics/project-health?projectId={guid} — GetProjectHealth

**Query**: `GetProjectHealthQuery(projectId, tenantId, now?: DateTime) : IQuery<Result<ProjectHealthResponse>>`

```json
// GET /api/metrics/project-health?projectId={guid}
// Response 200 — completed %, overdue/atRisk/blocked counts, deadlines, violations, milestone status
{
  "projectId": "guid",
  "completionPercent": 62,
  "total": 24,
  "active": 12,
  "overdue": 3,
  "atRisk": 2,
  "blocked": 1,
  "critical": 4,
  "upcomingDeadlines": [{ "workItemId": "guid", "title": "...", "dueDate": "2026-10-01T00:00:00Z", "deadlineStatus": "AtRisk" }],
  "violations": [{ "definitionId": "guid", "code": "delivery-date", "value": 62, "threshold": 80, "isViolated": true, "computedAt": "..." }],
  "milestones": [{ "id": "guid", "title": "M1", "status": "Planned", "dueDate": "...", "remainingWorkItemIds": ["guid"] }],
  "tenantId": "guid",
  "generatedAt": "2026-09-01T...",
  "progressExplanationRef": "/api/progress/workItemId/explanation"
}
```

**Authorization**: `IAuthorizationEvaluator.CanActorPerform(actor, project, projectHealth.read)` gated by subtree/membership — viewer without visibility to project gets `404` (not `403` leaking existence); zero cross-branch contribution.

## GET /api/dashboards/manager?managerId={guid} — GetManagerDashboard (subtree)

**Query**: `GetManagerDashboardQuery(managerId, tenantId, now?: DateTime) : IQuery<Result<ManagerDashboardResponse>>`

```json
// GET /api/dashboards/manager?managerId={guid}
// Response 200 — manager's Golden Rule A-visible projects aggregated
{
  "managerId": "guid",
  "tenantId": "guid",
  "totals": { "total": 48, "active": 22, "overdue": 5, "blocked": 2, "critical": 6 },
  "completionPercent": 58,
  "tasksBySubordinate": [
    { "subordinateId": "guid (Alice)", "total": 12, "active": 6, "overdue": 1, "completionPercent": 62 },
    { "subordinateId": "guid (Bob)", "total": 8, "overdue": 0, "completionPercent": 71 }
  ],
  "upcomingDeadlines": [{ "workItemId": "guid", "title": "...", "dueDate": "...", "assigneeId": "guid", "deadlineStatus": "Overdue" }],
  "projectHealth": [
    { "projectId": "guid", "completionPercent": 62, "violations": [{ "code": "delivery-date", "isViolated": true }] }
  ],
  "violations": [{ "definitionId": "guid", "code": "delivery-date", "value": 62, "threshold": 80, "isViolated": true, "projectId": "guid" }],
  "generatedAt": "2026-09-01T..."
}
```

**Authorization & aggregation**:

1. Resolve `allowedProjectIds = IsInSubtree(managerId, userId) ? subtreeProjectIds : explicit membershipProjectIds` via `IManagementHierarchy.GetSubtreeAsync(managerId)` + `IProjectMembership` (read `metrics`/`projects`? For dashboards, project visibility = manager is in project's manager subtree or is member). `TenantId` is first predicate.
2. Filter `WorkItem`/`MetricValue`/`Milestone` by `ProjectId IN allowedSet AND TenantId==ctx.TenantId` **before** `GroupBy/Count/Avg`. Metric violation join: `MetricValue.IsViolated → MetricThresholdViolated` events already persisted, so dashboards project `WHERE isViolated`.
3. Cross-branch manager's query returns disjoint `totals`/`tasksBySubordinate` (subtree isolation, SC-005). Manager without subtree/visibility gets `totals:0` not `403` leaking.

**Pagination/sorting**: dashboards are aggregates, not paginated; `upcomingDeadlines` limited to top 10 by `dueDate ASC`. `tasksBySubordinate` paginated if needed (page 1..20 max 50, default 20).

**Performance**: `GetProjectHealth` <200ms; `GetManagerDashboard` with 100 tasks across 5 subordinates <500ms p95 (FR-009 via EF `GroupBy` on `metrics`/`projects` join).

## Web — dashboard store/pages

- `src/Web/src/app/features/dashboard/dashboard.store.ts` — `signalStore(withState({projectHealth, managerDashboard}), withRequestStatus(), withLogger('dashboard'), withSelectedEntity(), withComputed({filtered}))` — health cards (`minimal-ui-design-system` elevated cards, `withLogger` logs state).
- `src/Web/src/app/features/dashboard/dashboard.component.ts` — consumes both queries; `isPending` shows skeleton, `error` shows banner, `isFulfilled` renders KPI cards + violations + milestones; no board mutation.
