# Quickstart: Metrics, Progress and Planning Validation

**Feature**: 004-metrics-progress-planning | **Date**: 2026-09-01 | **Depends on**: 003-projects-work-kanban (WorkItem, Milestone placeholder) + 002-identity (IManagementHierarchy, TenantContext) must be complete first

## Prerequisites

- 003 passed: `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `MetricsDbContext` schema `metrics` migrations applied (see Setup), `IManagementHierarchy` + `TenantContext` (002) reachable.
- `oroidentityserver` running (Authority via `Identity__Authority`); `tenant_id` on tokens.
- App running: `aspire run` or `dotnet run --project src/Api/Api.csproj` — `/health` <1s.
- This feature's migrations applied (once, after 003):

```bash
dotnet ef migrations add Metrics_004_Initial --project src/Modules/Metrics/Metrics.Infrastructure --startup-project src/Api/Api.csproj --context MetricsDbContext
dotnet ef database update --project src/Modules/Metrics/Metrics.Infrastructure --startup-project src/Api/Api.csproj --context MetricsDbContext

# Tests
dotnet test tests/Metrics.Tests -v minimal           # new: Unit (strategy determinism, zeroWeight, deadline midnight, explanation completeness) + Integration (version history, asOf, dashboard subtree, threshold→both models) + E2E (event→recalc→dashboard)
dotnet test tests/Architecture -v minimal
npm --prefix src/Web test -- --include="**/dashboard.store.spec.ts" # dashboard withRequestStatus
```

## Setup — seed project, metrics, subtasks

```bash
# 1) Tokens with distinct sub/tenant_id
MANAGER_TOKEN=<jwt sub=M tenant_id=T> # supervises Alice/Bob
OTHER_MANAGER_TOKEN=<jwt sub=M2> # disjoint subtree (Carol)
ALICE_TOKEN=<jwt sub=Alice>

# 2) Project (manager acts)
PROJECT_ID=$(curl -s -X POST http://localhost:5000/api/projects \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Revamp","managerId":"'"$(jq -r .sub <(echo $MANAGER_TOKEN | cut -d. -f2 | base64 -d))"'","status":"Active","priority":"High","criticality":"High"}' | jq -r .id)

# 3) Define metric per project (appends version 1)
DEF_ID=$(curl -s -X POST http://localhost:5000/api/metrics/definitions \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"projectId":"'"$PROJECT_ID"'","code":"delivery-date","name":"Delivery Date","dimension":"DeadlineAdherence","weight":0.3,"target":100,"threshold":80,"requiresEvidence":false}' | jq -r .id)

# Update → version 2 (append)
curl -s -X PUT http://localhost:5000/api/metrics/definitions/$DEF_ID \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"weight":0.5,"expectedVersion":1}' | jq . # → version 2, isCurrent true

# Historical: asOf before update returns version 1
curl -s "http://localhost:5000/api/metrics/definitions?projectId=$PROJECT_ID&code=delivery-date&asOf=2026-08-01" -H "Authorization: Bearer $MANAGER_TOKEN" | jq .version # → 1

# 4) Parent task + 4 weighted subtasks (spec SC-002 fixture)
PARENT_ID=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"Parent","type":"Task","priority":"High","criticality":"High"}' | jq -r .id)
for i in 1 2 3; do curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"Sub '$i'","type":"Subtask","parentId":"'"$PARENT_ID"'","priority":"Medium"}' | jq -r .id | while read sid; do echo sub $sid; curl -s -X POST http://localhost:5000/api/workitems/$sid/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"Planned","expectedVersion":1}' >/dev/null; curl -s -X POST http://localhost:5000/api/workitems/$sid/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"InProgress","expectedVersion":2}' >/dev/null; curl -s -X POST http://localhost:5000/api/workitems/$sid/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"InReview","expectedVersion":3}' >/dev/null; curl -s -X POST http://localhost:5000/api/workitems/$sid/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"Completed","expectedVersion":4}' >/dev/null; done; done
# One weighted subtask left at 0% (weight 2, not completed)

# Milestone linked to 2 work items
MILESTONE_ID=$(curl -s -X POST http://localhost:5000/api/planning/milestones -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"projectId":"'"$PROJECT_ID"'","title":"M1","dueDate":"2026-10-15T00:00:00Z","linkedWorkItemIds":["'"$PARENT_ID"'"]}' | jq -r .id)
```

## Verify — the 8 acceptance pillars (spec SC-001..008)

**SC-002 determinism + explanation (weighted 60%)**

```bash
curl -s -X POST http://localhost:5000/api/progress/$PARENT_ID/recalculate -H "Authorization: Bearer $MANAGER_TOKEN" | jq . # → resultPercent 60, weightsSum 5, components 4, strategy weightedSubtask
curl -s http://localhost:5000/api/progress/$PARENT_ID/explanation -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{resultPercent, weightsSum, strategyId, components: [.components[] | {name,weight,progress,contribution}]}'
# → 60, 5, weightedSubtask, 4 components (1+1+1+0)/5

# SC-001 determinism: compute twice → byte-identical
curl -s -X POST http://localhost:5000/api/progress/$PARENT_ID/recalculate -H "Authorization: Bearer $MANAGER_TOKEN" | jq .resultPercent # → 60 again
```

**Zero-weight edge (no crash)**

```bash
curl -s -X POST http://localhost:5000/api/metrics/definitions -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"projectId":"'"$PROJECT_ID"'","code":"zero-w","name":"Zero","dimension":"Quality","weight":0,"target":100,"threshold":80,"requiresEvidence":false}' | jq .weight # → 0
curl -s -X POST http://localhost:5000/api/progress/$PARENT_ID/recalculate -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{resultPercent, zeroWeight}' # with no components → 0, zeroWeight true
```

**SC-004 manual override audited**

```bash
curl -s -X POST http://localhost:5000/api/progress/$PARENT_ID/override -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"newProgress":90,"justification":"demo"}' | jq '{resultPercent, isOverride}'
# → 90, isOverride true
curl -s "http://localhost:5000/api/audit?resourceId=$PARENT_ID&action=audit.progress.overridden" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.[0] | {actor, previous, new, justification}' # → actor M, 60→90, demo
curl -s http://localhost:5000/api/progress/$PARENT_ID/explanation -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{isOverride, overrideJustification}' # → true, demo
# 403 without progress.override permission (other manager without membership)
curl -s -X POST http://localhost:5000/api/progress/$PARENT_ID/override -H "Authorization: Bearer $OTHER_MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"newProgress":10,"justification":"x"}' | jq .status # → 403 generic
```

**SC-007 deadline pure UTC midnight**

```bash
# task due tomorrow InProgress → OnTime; due in 2 days with 3-day window → AtRisk; due yesterday incomplete → Overdue; completed before due → CompletedOnTime; after → CompletedLate
curl -s "http://localhost:5000/api/planning/deadline?workItemId=$PARENT_ID&now=2026-09-01T00:00:00Z" -H "Authorization: Bearer $MANAGER_TOKEN" | jq .status # → OnTime|AtRisk|Overdue per dueDate
```

**SC-003 milestone + threshold violated → both dashboards**

```bash
curl -s -X POST http://localhost:5000/api/planning/milestones/$MILESTONE_ID/evaluate -H "Authorization: Bearer $MANAGER_TOKEN" | jq .status # → Slipped (not all linked Completed) or Reached when completed
# Threshold violated: completion 62% < 80% → MetricThresholdViolated
curl -s "http://localhost:5000/api/metrics/project-health?projectId=$PROJECT_ID" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.violations[] | {code, isViolated}' # → delivery-date true
curl -s "http://localhost:5000/api/dashboards/manager?managerId=$(jq -r .sub <(echo $MANAGER_TOKEN | cut -d. -f2 | base64 -d))" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.violations[] | {code, isViolated}' # also true
```

**SC-005 subtree isolation + SC-006 historical**

```bash
# Other manager sees disjoint totals
curl -s "http://localhost:5000/api/dashboards/manager?managerId=M2" -H "Authorization: Bearer $OTHER_MANAGER_TOKEN" | jq .totals # → totals 0 or disjoint from M
# Historical asOf
curl -s http://localhost:5000/api/progress/$PARENT_ID/explanation?asOf=2026-08-01 -H "Authorization: Bearer $MANAGER_TOKEN" | jq .computedAt # → version active at that date, not latest
```

**SC-008 explanation presence**

```bash
curl -s http://localhost:5000/api/progress/$PARENT_ID/explanation -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{strategyId, weightsSum, components: (.components|length), inputsSnapshot: (.inputsSnapshot != null)}' # all present, else failure
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `404 project not found` | tenant mismatch or projectId wrong | Verify `TenantContext` from `tenant_id` claim and route `projectId` |
| `threshold not violated` | MetricValue not evaluated yet | `POST /progress/recalculate` then `GET /metrics/project-health` (evaluation triggered on recalc or event) |
| `subtree includes other manager` | `IManagementHierarchy.GetSubtree` stub returns all | Verify `Organization.Contracts/IManagementHierarchy` mock returns only subtree projectIds, not all |
| `asOf returns latest not historical` | explanation append index missing | Verify `IX_progress_explanations_workItem_computedAt` and `ExplainProgress` queries `WHERE computedAt <= asOf ORDER BY computedAt DESC LIMIT 1` |
| `deadline AtRisk wrong` | local time vs UTC | Verify `IDeadlineEvaluator` truncates to `Date` UTC vs `nowUtc.Date` |
| `override 403 even for manager` | `progress.override` permission not granted to role | Verify `Identity` role→permission `progress.override` seeded for `ProjectManager`/`Manager` |
