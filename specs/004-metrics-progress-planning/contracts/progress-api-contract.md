# Contract: Progress API

**Module**: `Metrics` (BC-04) | **Base path**: `/api/progress` | **Auth**: Bearer JWT (`tenant_id`) | **Determinism**: Same `InputsSnapshot`+`strategyId` → byte-identical `ProgressExplanation`.

## POST /api/progress/{workItemId}/recalculate — RecalculateProgress (on-demand)

**Command**: `RecalculateProgressCommand(workItemId, tenantId, actorId) : ICommand<Result<ProgressExplanationResponse>>`

```json
// POST /api/progress/{workItemId}/recalculate → no body, actor from ClaimsPrincipal
// Response 200
{
  "workItemId": "guid",
  "projectId": "guid",
  "strategyId": "weightedSubtask",
  "computedAt": "2026-09-01T12:00:00Z",
  "resultPercent": 60,
  "weightsSum": 5,
  "zeroWeight": false,
  "isOverride": false,
  "components": [
    { "name": "Subtask: impl due-date indicator", "weight": 1, "progress": 100, "contribution": 1 },
    { "name": "Subtask: overdue badge", "weight": 2, "progress": 0, "contribution": 0 }
  ],
  "inputsSnapshot": {
    "subtasks": [{ "workItemId": "guid", "progress": 100, "completed": true }],
    "evidenceIds": [],
    "milestoneIds": []
  }
}
```

**Domain**: Handler resolves `project.strategyId` → `IStrategyResolver.Get(strategyId)` → `IWorkItemSnapshotProvider.GetSubtasksAsync` → `strategy.Calculate(inputs)` → append `ProgressExplanation` row (`metrics.progress_explanations`) → outbox `ProgressRecalculatedIntegrationEvent`. Idempotent — re-running with same snapshot overwrites same `computedAt` second? No, appends new row with new `computedAt`, but determinism test replays same `InputsSnapshot`.

Also triggered automatically via RabbitMQ subscriber `WorkItemStatusChangedIntegrationEvent` / `WorkItemCompletedIntegrationEvent` (idempotent handler, same path).

## GET /api/progress/{workItemId}/explanation?asOf= — ExplainProgress (history)

**Query**: `ExplainProgressQuery(workItemId, tenantId, asOf?: DateTime) : IQuery<Result<ProgressExplanationResponse>>`

```
GET /api/progress/{workItemId}/explanation           → latest (MAX computedAt)
GET /api/progress/{workItemId}/explanation?asOf=2026-08-01 → version where computedAt <= asOf ORDER BY computedAt DESC LIMIT 1
```

Same envelope as above; historical reconstructibility (FR-011) — append-only `metrics.progress_explanations` indexed `(workItemId, computedAt DESC)`.

## POST /api/progress/{workItemId}/override — OverrideProgressManually (audited, permissioned)

**Command**: `OverrideProgressManuallyCommand(workItemId, tenantId, newProgress 0–100, justification 1–500, actorId) : ICommand<Result<ProgressExplanationResponse>>`

```json
// POST /api/progress/{workItemId}/override
{
  "newProgress": 90,
  "justification": "demo override for stakeholder review",
  "expectedVersion": 1
}
// Response 200 — next ExplainProgress isOverride=true
{
  "resultPercent": 90,
  "isOverride": true,
  "overrideJustification": "demo override",
  "overrideActorId": "guid (actor sub)",
  "components": [{ "name": "Manual", "weight": 1, "progress": 90, "contribution": 90, "isOverride": true }],
  "audit": { "actor": "guid", "previous": 60, "new": 90, "justification": "demo override" }
}
// Errors: 403 Forbidden generic (no leak, requires permission progress.override via IAuthorizationEvaluator + subtree/membership), audited as authorization.denied; 400 Validation (progress 0–100, justification required); 409 Conflict (Stale RowVersion)
```

**Domain**: `CheckRule` manual value, `IAuthorizationEvaluator.CanActorPerform(progress.override)` before policy, append `ProgressExplanation` with `isOverride=true, overrideJustification, overrideActorId`, stage same-tx outbox `audit.progress.overridden` (append-only) + `ProgressOverriddenIntegrationEvent` for notifications. Next `ExplainProgress` includes the override component as source.

No direct `PUT /progress` — manual path is the only mutation.

## GET /api/progress/strategies — ListStrategies

Returns `[{strategyId: "weightedSubtask", name: "Weighted Subtask"}, {strategyId: "deliverableMilestone", name: "Deliverable Milestone"}]` — selection per project via `PATCH /api/projects/{id}/progressStrategy {strategyId}` (thin slice in Metrics, updates `metrics.project_settings`).
