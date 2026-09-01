# Contract: Planning API (Milestones + Deadline)

**Module**: `Metrics` (BC-04) | **Base path**: `/api/planning` | **DeadlineStatus**: VO not UI string, pure UTC.

## POST /api/planning/milestones — CreateMilestone

**Command**: `CreateMilestoneCommand : ICommand<Result<MilestoneResponse>>`

```json
// Request
{
  "projectId": "guid",
  "title": "M1 — Design sign-off",
  "dueDate": "2026-10-15T00:00:00Z",
  "linkedWorkItemIds": ["guid", "guid"],
  "criteria": { "requiredStatus": "Completed" }
}
// Response 201 — Location: /api/planning/milestones/{id}
{
  "id": "guid (MilestoneId)",
  "projectId": "guid",
  "title": "M1 — Design sign-off",
  "dueDate": "2026-10-15T00:00:00Z",
  "linkedWorkItemIds": ["guid", "guid"],
  "status": "Planned",
  "version": 1,
  "isCurrent": true,
  "tenantId": "guid"
}
// Errors: 400 Validation (title 3–100, dueDate required, linked items must be in same ProjectId else CrossProject →400 unless policy allows), 403 Forbidden (planning.manage), 404 project not found
```

**Domain**: `Milestone.Create(...)` → version 1 `Planned` → `MilestoneCreated` → outbox. Versioned append — update creates `version+1` with new `DueDate`/`LinkedWorkItemIds` and `IsCurrent` flip.

## PUT /api/planning/milestones/{id} — UpdateMilestone (versioned)

```json
{ "title": "M1 — Design sign-off (slipped)", "dueDate": "2026-11-01T00:00:00Z", "expectedVersion": 1 }
// → new version row, previous retained for historical
```

## POST /api/planning/milestones/{id}/evaluate — EvaluateMilestone

**Command**: `EvaluateMilestoneCommand(milestoneId, tenantId) : ICommand<Result<MilestoneEvaluateResponse>>`

```json
// POST /api/planning/milestones/{id}/evaluate → no body
// Response 200
{
  "milestoneId": "guid",
  "status": "Reached", // or Slipped
  "reachedAt": "2026-09-01T...",
  "remainingWorkItemIds": [],
  "linkedStatuses": [{ "workItemId": "guid", "status": "Completed" }]
}
// Also emits MilestoneReached or MilestoneSlipped via outbox integration event for dashboards/notifications.
```

**Criteria** (FR-008): explicit — default `all linked WorkItems status == Completed (statusId == Completed)` plus if milestone `requiresEvidence` then evidence approved. Criteria is part of `ProgressExplanation` when milestone contributes as component.

## GET /api/planning/milestones?projectId= — List Milestones (versioned)

```
GET /api/planning/milestones?projectId={guid}&includeHistory=false → current only (IsCurrent=true)
GET /api/planning/milestones?projectId={guid}&asOf=2026-08-01 → version active at date
```

## GET /api/planning/deadline?workItemId=&now= — DeadlineStatus (pure)

**Query**: `EvaluateDeadlineQuery(workItemId, tenantId, nowUtc?: DateTime, atRiskWindowDays?: int) : IQuery<Result<DeadlineStatusResponse>>`

```json
// GET /api/planning/deadline?workItemId={guid}&now=2026-09-01T00:00:00Z
// Response 200 — VO, not UI string
{
  "workItemId": "guid",
  "dueDate": "2026-09-03T00:00:00Z",
  "status": "AtRisk", // OnTime(1)|AtRisk(2)|Overdue(3)|CompletedOnTime(4)|CompletedLate(5)
  "statusId": 2,
  "atRiskWindowDays": 3,
  "now": "2026-09-01T00:00:00Z"
}
// Pure: IDeadlineEvaluator Evaluate(dueDate, statusId, completedAt, atRiskWindowDays, nowUtc)
```

**Rules** (FR-006, deterministic UTC midnight): `dueDate==null→OnTime`; `status==Completed` → `completedAt <= dueDate.Date ? CompletedOnTime : CompletedLate`; else if `dueDate.Date < now.Date → Overdue`; else if `(dueDate.Date - now.Date).Days <= atRiskWindowDays → AtRisk`; else `OnTime`. `atRiskWindowDays` per `metrics.project_settings` (default 3).
