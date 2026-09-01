# Quickstart: Projects, Work Items and Kanban Validation

**Feature**: 003-projects-work-kanban | **Date**: 2026-09-01 | **Depends on**: 002-identity-access-organization (Organization hierarchy + evaluator, TenantContext, audit outbox) must be complete first

## Prerequisites

- Foundation 002 passed: `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `OroKanban.AppHost` declares `postgres`/`rabbitmq`/`redis` + external `identity-api` (authority via `Identity__Authority`).
- 002 applied: `dotnet ef database update --project src/Modules/Organization/Organization.Infrastructure/... --startup-project src/Api/Api.csproj` (schema `organization`).
- `oroidentityserver` Podman container running and reachable at Authority (`GET {Authority}/.well-known/openid-configuration`); client for `authorization_code` + `refresh_token` registered out-of-band; `tenant_id` claim present on tokens.
- App running: `aspire run` (or `dotnet run --project src/Api/Api.csproj`) — all resources Healthy, `/health` and `/alive` <1 s.
- This feature's migrations applied (once, after 002):

```bash
dotnet ef migrations add Projects_003_Initial --project src/Modules/Projects/Projects.Infrastructure/Projects.Infrastructure.csproj --startup-project src/Api/Api.csproj
dotnet ef database update --project src/Modules/Projects/Projects.Infrastructure/Projects.Infrastructure.csproj --startup-project src/Api/Api.csproj

# Verify seeded enumerations
curl -s http://localhost:5000/api/projects/enumerations/workitem-status -H "Authorization: Bearer <token>" | jq .
# Expected: Backlog, Planned, InProgress, Blocked, InReview, Completed

# Tests
dotnet test tests/Architecture -v minimal
dotnet test src/Modules/Projects/Projects.Tests -v minimal           # new: Unit + Integration + E2E (below)
```

## Setup — seed a project and a hierarchy

```bash
# 1) Obtain manager + member tokens (from OroIdentityServer) — extract sub claims as userIds
MANAGER_TOKEN=<jwt with sub=M and tenant_id=T>
MEMBER_A1_TOKEN=<jwt>
MEMBER_B_TOKEN=<jwt for an out-of-subtree, out-of-project user>
AUDITOR_TOKEN=<jwt with role Auditor>

# 2) Create a project (manager acts)
PROJECT_ID=$(curl -s -X POST http://localhost:5000/api/projects \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Revamp checkout","managerId":"<M-sub>","status":"Active","priority":"High","criticality":"High"}' | jq -r .id)

# 3) Add member A1 (who is in manager subtree) and do not add B (cross-branch, no membership)
curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/members \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"userId":"<A1-sub>","role":"Contributor"}' | jq .

# 4) Create an Epic → Feature → Task hierarchy
EPIC_ID=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Checkout epic","type":"Epic","priority":"High","criticality":"High"}' | jq -r .id)

FEATURE_ID=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Payment feature","type":"Feature","parentId":"'$EPIC_ID'","priority":"High","criticality":"Medium"}' | jq -r .id)

TASK_ID=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Add retry","type":"Task","parentId":"'$FEATURE_ID'","priority":"High","criticality":"Medium","dueDate":"2026-10-01T00:00:00Z","estimatedEffortHours":8,"tags":["kanban","ui"]}' | jq -r .id)

# Verify each create: version 1 + WorkItemCreated outbox + detail
curl -s http://localhost:5000/api/workitems/$TASK_ID -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{title, status, type, version, parentId}'
# Expected: { title:"Add retry", status:"Backlog", type:"Task", version:1, parentId: FEATURE_ID }

# Verify taxonomy validation — bad type rejected
curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Bad","type":"UnknownType","priority":"Medium"}' | jq .
# Expected: 400 Validation, no row inserted
```

## Verify — the six acceptance pillars

### 1. Project + work item creation (SC-001)

After the task above, two reads prove it:

```bash
curl -s http://localhost:5000/api/workitems/$TASK_ID -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.version'
# → 1
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.columns[] | select(.status=="Backlog") | .items | length'
# → 1 and TASK_ID appears there
# Outbox (inspect via Aspire pgadmin or outbox table): one WorkItemCreatedIntegrationEvent staged in same tx
```

### 2. Invalid transition rejected, board unchanged (SC-002)

```bash
# Attempt Backlog → Completed (no allowed path) — valid target is Planned
curl -s -X POST http://localhost:5000/api/workitems/$TASK_ID/status \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"targetStatus":"Completed","expectedVersion":1}' | jq .
# Expected: 400 { code:"validation", detail:"Transition not allowed: Backlog → Completed" } — no WorkItemStatusChanged, no status change

curl -s http://localhost:5000/api/workitems/$TASK_ID -H "Authorization: Bearer $MANAGER_TOKEN" | jq .status
# → "Backlog" (unchanged)
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.columns[] | select(.status=="Backlog") | .items[] | .id'
# → TASK_ID still in Backlog column

# Allowed path succeeds: Backlog → Planned → InProgress
curl -s -X POST http://localhost:5000/api/workitems/$TASK_ID/status \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"targetStatus":"Planned","expectedVersion":1}' | jq .
curl -s -X POST http://localhost:5000/api/workitems/$TASK_ID/status \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"targetStatus":"InProgress","expectedVersion":2}' | jq .status
# → "InProgress" plus WorkItemStatusChanged outbox + audit entry
```

### 3. Cycle prevention (SC-003)

```bash
WI_A=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"A","type":"Task","priority":"Medium"}' | jq -r .id)
WI_B=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"B","type":"Task","priority":"Medium"}' | jq -r .id)
WI_C=$(curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"C","type":"Task","priority":"Medium"}' | jq -r .id)

curl -s -X POST http://localhost:5000/api/workitems/$WI_A/dependencies -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"principalId":"'$WI_B'","type":"Blocks"}' | jq .type
curl -s -X POST http://localhost:5000/api/workitems/$WI_B/dependencies -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"principalId":"'$WI_C'","type":"Blocks"}' | jq .type

# Closing the cycle — must be rejected
curl -s -X POST http://localhost:5000/api/workitems/$WI_C/dependencies -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"principalId":"'$WI_A'","type":"Blocks"}' | jq .
# Expected: 400 Validation — Circular dependency (CircularDependencyRule), no DependencyAdded, graph remains A→B→C; response time <200 ms

# RelatedTo is allowed to form a long chain and never participates in cycle check
curl -s -X POST http://localhost:5000/api/workitems/$WI_C/dependencies -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"principalId":"'$WI_A'","type":"RelatedTo"}' | jq .type
# Expected: 201 — RelatedTo

# Hierarchy reparent — descendant check
curl -s -X POST http://localhost:5000/api/workitems/$EPIC_ID/reparent -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"newParentId":"'$TASK_ID'","expectedVersion":1}' | jq .
# Expected: 400 — cannot reparent to descendant (ReparentNoCycleRule)
```

### 4. Optimistic concurrency (SC-004)

```bash
V=$(curl -s http://localhost:5000/api/workitems/$WI_A -H "Authorization: Bearer $MANAGER_TOKEN" | jq .version)
# Two concurrent saves from same base version — run in parallel (e.g., two terminals or background curl)
curl -s -X POST http://localhost:5000/api/workitems/$WI_A/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"Planned","expectedVersion":'$V'}' | jq .status &
curl -s -X POST http://localhost:5000/api/workitems/$WI_A/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"Planned","expectedVersion":'$V'}' | jq .
# Expected: exactly one 200, the other 409 { code:"conflict", currentVersion: V+1 } — never silent overwrite; <1 s total
```

### 5. Assignment — subtree or shared membership, active, not completed (SC-005 / Story 4)

```bash
# Valid: manager assigns TASK_ID (InProgress so not Completed) to subtree member A1
curl -s -X POST http://localhost:5000/api/workitems/$TASK_ID/assign \
  -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" \
  -d '{"assigneeId":"<A1-sub>","expectedVersion":3}' | jq .
# Expected: 200 { responsibleId:<A1-sub>, version:4 } + WorkItemAssigned → outbox integration event + audit + GET /api/users/me/tasks for A1 includes TASK_ID

# Denied: out-of-subtree user B without project membership attempts to assign or is assigned to
curl -s -X POST http://localhost:5000/api/workitems/$TASK_ID/assign \
  -H "Authorization: Bearer $MEMBER_B_TOKEN" -H "Content-Type: application/json" \
  -d '{"assigneeId":"<A1-sub>","expectedVersion":4}' | jq .
# Expected: 403 generic { title:"Forbidden" } with no detail, no WorkItemAssigned emitted, audited as authorization.denied (inspect audit outbox/store):
curl -s "http://localhost:5000/api/audit?resourceId=$TASK_ID&action=authorization.denied" -H "Authorization: Bearer $AUDITOR_TOKEN" | jq '.[0] | {actor, permission, tenant, correlationId}'
# Expected: actor=B, permission=workitem.assign, tenant present, correlationId non-empty

# Rejected: Completed item cannot be assigned
curl -s -X POST http://localhost:5000/api/workitems/$WI_B/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"Planned","expectedVersion":1}' | jq .
# ... advance WI_B to InProgress→InReview→Completed, then:
curl -s -X POST http://localhost:5000/api/workitems/$WI_B/assign -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"assigneeId":"<A1-sub>","expectedVersion":<completed-version>}' | jq .
# Expected: 400 Validation — work item is completed (WorkItemNotCompletedRule)
```

### 6. Audit + notification integration (SC-006) and board E2E (SC-008)

```bash
# Any successful status or assignment above should have produced an audit entry and — where relevant — a notification integration event
# via the outbox in the same transaction:
curl -s "http://localhost:5000/api/audit?resourceId=$TASK_ID" -H "Authorization: Bearer $AUDITOR_TOKEN" | jq '.[] | {action, actor, traceId}'
# Expected: entries for WorkItemCreated, WorkItemStatusChanged, WorkItemAssigned (append-only)

# E2E drag/drop chain: board query → ChangeWorkItemStatus (valid) → board re-query
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.columns[] | {status, count}'
curl -s -X POST http://localhost:5000/api/workitems/$WI_A/status -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"targetStatus":"InReview","expectedVersion":<current>}' | jq .status
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.columns[] | select(.status=="InReview") | .items[] | .id'
# Expected: WI_A now appears in InReview column end-to-end <1 s

# Board correctness + authorization (SC-007): 50 seeded items
for i in $(seq 1 50); do curl -s -X POST http://localhost:5000/api/projects/$PROJECT_ID/workitems -H "Authorization: Bearer $MANAGER_TOKEN" -H "Content-Type: application/json" -d '{"title":"seed '$i'","type":"Task","priority":"Medium"}' >/dev/null; done
time curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board?pageSize=50" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '{totalCount, overdueCount, counts: [.columns[] | {status: .status, count: .count}]}'
# Expected: totalCount==50+, p95 <500 ms

# Filters + swimlanes + overdue
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board?swimlane=assignee&sortBy=criticality&sortDir=desc" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.swimlanes[0] | {key, label, columnCounts: [.columns[] | .count]}'
curl -s "http://localhost:5000/api/projects/$PROJECT_ID/board?status=Backlog&assignee=<A1-sub>" -H "Authorization: Bearer $MANAGER_TOKEN" | jq '.columns[] | select(.status=="Backlog") | .count'
# Expected: swimlanes grouped by responsibleId (Unassigned lane present if any unassigned), board items show progress badge, criticality mapping, isOverdue when DueDate < today
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `404 project not found` on valid id | `HasDefaultSchema("projects")` missing or migration not applied | `dotnet ef database update` for `ProjectsDbContext`; verify tables `projects.projects`, `projects.work_items` exist in `pgadmin` |
| Allowed transition rejected | `IWorkItemTransitionPolicy` map not seeded / `TransitionIsAllowedRule` not wired via `CheckRule` | Verify policy registration in `Projects.Infrastructure` and that `WorkItem.ChangeStatus` calls `CheckRule` |
| Cycle not rejected | `IDependencyCycleDetector` not filtering `RelatedTo` or not loading all project edges before DFS | Ensure handler loads `nonRelatedTo edges for projectId` then calls detector; add unit test for 3-node cycle |
| Assignment always 403 even for subtree | `IManagementHierarchy` not injected or stub returns false | Verify `Organization.Infrastructure` reference + `AddScoped<IManagementHierarchy>`; check `GetSubtree(managerId)` returns the member ids |
| Concurrency overwrite instead of 409 | `RowVersion` missing `IsRowVersion()` or `Version` not checked | Verify `projects.work_items.row_version bytea` has `IsRowVersion()` in model and handler catches `DbUpdateConcurrencyException → Error.Conflict` |
| Board returns cross-branch items | Authorization `Specification<T>` composed after `ListAsync` | Move `And(AuthorizedSpec)` before fetch — check handler ordering (see kanban-board-contract Authorization section) |
| Board empty despite items | `tenantId` claim not propagated or `projectId` filter wrong | Verify `TenantContext` populated from JWT `tenant_id`; request route `projectId` matches `Project.TenantId` |
| `400 missing projectId` on board | Query called without route | Call `GET /api/projects/{projectId}/board` — bare `GET /api/board` must return 400 |

## What is NOT validated here

- Rich progress derivation beyond stored `ProgressValue` → SPEC-004 Metrics.
- Full document lifecycle → BC-06 Documents.
- Real-time board push (poll/query only).
- Search/indexing across work items → BC-07 Search (EF read model here, not index).
