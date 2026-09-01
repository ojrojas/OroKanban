# Quickstart: Audit, Monitoring and Compliance Validation

**Feature**: 007-audit-monitoring-compliance | **Date**: 2026-09-01 | **Depends on**: 001-foundation (BuildingBlocks outbox, AppDbContextBase, ISender), 002-identity-access-organization (TenantContext, IManagementHierarchy, IAuthorizationEvaluator), 005-document-management (Document* events + IDocumentAccessPolicy), 006-llm-document-intelligence (Llm* events + IsSafe) — all must be complete first; `AuditDbContext` schema `audit` + `audit_consumed_events` + RabbitMQ `audit.*` topic + OTel `AddServiceDefaults` required.

## Prerequisites

- 006 passed: `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `AuditDbContext` + `OrganizationDbContext`/`DocumentsDbContext`/`AiProcessingDbContext` migrations applied, `IManagementHierarchy` + `TenantContext` reachable, `IOutboxWriter` + `OutboxProcessor` polling `outbox_messages`.
- `oroidentityserver` running (Authority via `Identity__Authority`); JWTs carry `sub` + `tenant_id` + `roles` (`auditor`, `manager`, `viewer`).
- App running: `aspire run` or `dotnet run --project src/Api/Api.csproj` — `/health` <1s, Aspire dashboard shows `postgres`, `rabbitmq`, `redis`, `ai_provider`, `vector_store` (InMemory) health entries.
- This feature's migrations applied (once, after 002..006):

```bash
dotnet ef migrations add Audit_007_Initial --project src/Modules/Audit/Audit.Infrastructure --startup-project src/Api/Api.csproj --context AuditDbContext
dotnet ef database update --project src/Modules/Audit/Audit.Infrastructure --startup-project src/Api/Api.csproj --context AuditDbContext
# Verify REVOKE (if no hash chaining per ADR-007-01): DB app role has no UPDATE/DELETE on audit.audit_entries
psql $DATABASE_URL -c "\z audit.audit_entries" # → app_orokanban: a=r--p (no w)

# Tests
dotnet test tests/Audit.Tests -v minimal           # new: Unit (MaskingPolicy, QueryAuthorizationComposition, EntryImmutability), Integration (ConsumerIdempotency, SearchFilters 8 dims, CorrelationPropagation, CrossBranchAuditSearch), E2E (DocumentWorkflowTimeline 7 entries)
dotnet test tests/Architecture -v minimal          # extended: AuditEntry has zero public setters, IRepository<AuditEntry> only AddAsync, all Audit queries via IAuditQueryAuthorization, tenant predicate required
```

---

## Setup — seed tenants, branches, projects, and correlation propagation

```bash
# Tokens with distinct sub/tenant_id/roles/organization subtrees
ALICE_TOKEN=<jwt sub=Alice tenant_id=T roles=[manager,auditor]>  # Alice subtree Org_A owns P_A
BOB_TOKEN=<jwt sub=Bob tenant_id=T roles=[viewer]>               # Bob in Org_A? no — Bob in branch B? For cross-branch test, Bob is in branch A but not auditor for B
CAROL_TOKEN=<jwt sub=Carol tenant_id=T roles=[manager]>          # Carol manages Alice (Alice's ancestor per Organization hierarchy)
AUDITOR_A_TOKEN=<jwt sub=AuditorA tenant_id=T roles=[auditor]>   # Auditor_A scoped to subtree Org_A (via IManagementHierarchy)
AUDITOR_ALL_TOKEN=<jwt sub=SuperAuditor tenant_id=T roles=[auditor]> # tenant-wide (all orgs)
OTHER_TENANT_TOKEN=<jwt sub=Zed tenant_id=T2 roles=[auditor]>

# Projects P_A (in Org_A subtree, owned by Alice) and P_B (in Org_B)
PROJECT_A=$(curl -s -X POST http://localhost:5000/api/projects -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" -d '{"name":"Audit Plan A","managerId":"<Alice sub>"}' | jq -r .id)
PROJECT_B=$(curl -s -X POST http://localhost:5000/api/projects -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" -H "Content-Type: application/json" -d '{"name":"Plan B","managerId":"<SuperAuditor>"}' | jq -r .id)

# Hierarchy: Alice's manager is Carol via PUT /api/organization/management-relationships (SPEC-002) — gives Carol subtree over Alice
# Audit search scope: Auditor_A sees only Org_A subtree + P_A; SuperAuditor sees all

# CorrelationId propagation test setup: clear existing audit, note Generation via X-Correlation-Id header
CID=$(uuidgen)
```

---

## Verify — the 5 acceptance pillars (spec SC-001..005)

### SC-001 — Catalog completeness (22 actions → one AuditEntry each, CorrelationId, masked)

```bash
CID=$(uuidgen)
# 1. AuthenticationFailed (wrong password) with CID
curl -s -X POST http://localhost:5000/api/auth/login -H "X-Correlation-Id: $CID" -H "Content-Type: application/json" -d '{"username":"alice","password":"wrong"}' | jq . # → 401

# 2. AuthorizationDenied (Bob tries to GetDocument he cannot read)
DOC_ID=<from 005 upload, IsSafe=true, Confidential>
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $BOB_TOKEN" -H "X-Correlation-Id: $CID" | jq . # → 403

# 3. ProjectCreated (Alice creates project)
curl -s -X POST http://localhost:5000/api/projects -H "Authorization: Bearer $ALICE_TOKEN" -H "X-Correlation-Id: $CID" -H "Content-Type: application/json" -d "{\"name\":\"Audit Project $CID\"}" | tee /tmp/proj.json
PROJ_ID=$(jq -r .id /tmp/proj.json)

# 4. DocumentApproved (Alice uploads then approves via 005 lifecycle)
# ... (005 SC-007 flow with X-Correlation-Id: $CID)

# 5. LlmResultApproved (Carol approves AI result with CID)
# ... (006 SC-003 flow with X-Correlation-Id: $CID)

sleep 2  # outbox→audit consumer <2s
curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID&page=1&pageSize=50" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '{totalCount, items: [.items[] | {action, actor, resourceType, result, correlationId}]}'
# → totalCount >=5, each action in {AuthenticationFailed, AuthorizationDenied, ProjectCreated, DocumentApproved, LlmResultApproved} has AuditId, Timestamp≈now, Actor==performer, ResourceType/ResourceId==target, Result==Success|Denied|Failed, CorrelationId==$CID, BeforeAfterSnapshot masked (ApiKey==*** if present)
# Mask check:
curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '.items[] | .beforeAfterSnapshot' | grep -q '"***"' && echo "masked ok"
# Duplicate delivery test (consumer idempotency):
# Re-publish same IntegrationEvent Id (simulate via direct RabbitMQ publish with same EventId) → count unchanged
CID_DUP=$CID
# (in test: AuditEventConsumer.HandleAsync same EventId twice → count ==1)
psql $DATABASE_URL -c "SELECT count(*) FROM audit.audit_consumed_events WHERE correlation_id='$CID'" # → >=5 distinct EventIds, no duplicate AuditEntry for same EventId
```

### SC-002 — Immutability (no setters, corrections new entries, hash chain)

```bash
# Domain immutability: reflection check
dotnet test tests/Audit.Tests --filter AuditEntryIsImmutable -v minimal # → pass (zero public setters, IRepository only AddAsync)

# Attempt to mutate via repository (compile-time impossibility already, but runtime via DbContext):
AUDIT_ID=$(curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq -r '.items[0].auditId')
# No PUT/PATCH /api/audit/entries/{id} exists — architecture has no Update endpoint (append-only)
curl -s -X PUT http://localhost:5000/api/audit/entries/$AUDIT_ID -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" -d '{"action":"Tampered"}' | jq . # → 404 (no route) or 405

# Correction as new entry:
curl -s -X POST http://localhost:5000/api/audit/corrections -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" -H "Content-Type: application/json" -d '{"correctedAuditId":"'$AUDIT_ID'","correctedResult":"Success","rationale":"Wrong result, fixing"}' | jq . # → 201 with Action=AuditCorrected, ResourceId=$AUDIT_ID
curl -s "http://localhost:5000/api/audit/trail/Document/$PROJ_ID" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '.items[] | {action, resourceId}' # → shows both original and AuditCorrected, original unchanged

# Hash chain verify (if ADR-007-01 adopted chaining):
curl -s "http://localhost:5000/api/audit/verify-chain?tenantId=T" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq . # → {valid: true} or {valid:false, firstMismatch:{auditId, expectedHash, actualHash}} if tampered
# Direct SQL UPDATE attempt (if REVOKE not chaining):
psql $DATABASE_URL -c "UPDATE audit.audit_entries SET action='Tampered' WHERE audit_id='$AUDIT_ID'" # → ERROR: permission denied for table audit_entries (REVOKE) or hash mismatch on VerifyChain
```

### SC-003 — Authorization-filtered search (cross-branch filtered out)

```bash
# Seed: Alice subtree Org_A owns P_A with entries E_A1( ProjectCreated) E_A2(DocumentAccessDenied on P_A), Org_B owns P_B with E_B1
# As Auditor_A scoped to Org_A subtree + P_A:
curl -s "http://localhost:5000/api/audit/entries?organizationId=<Org_A>&page=1&pageSize=50" -H "Authorization: Bearer $AUDITOR_A_TOKEN" | jq '{totalCount, orgs: [.items[].organizationId] | unique}'
# → totalCount for Org_A only, orgs == [<Org_A>], zero Org_B
curl -s "http://localhost:5000/api/audit/trail/Project/$PROJECT_A" -H "Authorization: Bearer $AUDITOR_A_TOKEN" | jq .totalCount # → >=1
curl -s "http://localhost:5000/api/audit/trail/Project/$PROJECT_B" -H "Authorization: Bearer $AUDITOR_A_TOKEN" | jq . # → 404 shadow (not 403), totalCount=0 (no leak that P_B exists)
# SuperAuditor sees all:
curl -s "http://localhost:5000/api/audit/entries?organizationId=<Org_B>" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq .totalCount # → >=1 for Org_B
# Filter combinations:
curl -s "http://localhost:5000/api/audit/entries?actorId=<Alice sub>&action=ProjectCreated&resourceType=Project&projectId=$PROJECT_A&organizationId=<Org_A>&from=2026-08-25T00:00:00Z&to=2026-09-01T23:59:59Z&result=Success&correlationId=$CID" -H "Authorization: Bearer $AUDITOR_A_TOKEN" | jq . # → filtered correctly, each filter AND
# Unauthenticated → 403 and audited as AuditSearchDenied:
curl -s "http://localhost:5000/api/audit/entries?projectId=$PROJECT_A" | jq . # → 401/403
# Other tenant → 404 shadow:
curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID" -H "Authorization: Bearer $OTHER_TENANT_TOKEN" | jq . # → 404 (never 403), totalCount=0
```

### SC-004 — Correlation timeline (7-entry distributed workflow reconstructible)

```bash
CID=$(uuidgen)
# Start workflow with CID propagated via X-Correlation-Id across 7 steps:
curl -s -X POST http://localhost:5000/api/documents -H "Authorization: Bearer $ALICE_TOKEN" -H "X-Correlation-Id: $CID" -F "file=@contract.pdf;type=application/pdf" -F "name=contract.pdf" | jq .documentId | xargs -I {} echo "DOC_ID={}"
# Queue LLM op with same CID (client propagates same X-Correlation-Id):
curl -s -X POST http://localhost:5000/api/ai/operations -H "Authorization: Bearer $ALICE_TOKEN" -H "X-Correlation-Id: $CID" -H "Content-Type: application/json" -d '{"documentId":"'$DOC_ID'","operationType":"Summarization"}' | jq .operationId
# Poll until LlmReviewCreated:
sleep 5
curl -s "http://localhost:5000/api/audit/timeline/$CID" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '{totalCount, items: [.items[] | {action, timestamp}]}'
# → totalCount==7, items ordered Timestamp asc: DocumentUploaded → DocumentProcessingStageCompleted(Validation) → DocumentProcessingStageCompleted(Storage) → DocumentApproved (if 005 lifecycle) → LlmOperationQueued → LlmResultGenerated → LlmReviewCreated (order matches actual workflow, 7 entries, each with Actor, Action, ResourceType/ResourceId, Result, BeforeAfterSnapshot masked, CorrelationId==$CID)
# Verify OTel baggage same:
# Check traceId == CID? No — OTel TraceId is separate W3C trace-id, but Baggage CorrelationId equals CID for audit timeline
```

### SC-005 — Health per dependency (identifiable)

```bash
# All healthy:
curl -s http://localhost:5000/health | jq . # → {status:"Healthy", entries:{postgres:{status:"Healthy"}, rabbitmq:{status:"Healthy"}, redis:{status:"Healthy"}, ai_provider:{status:"Healthy"}, vector_store:{status:"Healthy"}}, totalDuration}
curl -s http://localhost:5000/alive | jq . # → {status:"Healthy", entries:{self:{status:"Healthy"}}}

# Simulate postgres down (stop postgres container or stub Npgsql to throw SocketException via test helper):
# In test: HealthPerDependencyTests inject fault for postgres only
curl -s http://localhost:5000/health | jq '{status, entries: {postgres: .entries.postgres.status, rabbitmq: .entries.rabbitmq.status}}'
# → {status:"Unhealthy", entries:{postgres:"Unhealthy", rabbitmq:"Healthy"}} — distinguishable, not aggregated 503 alone
# Exception masked:
curl -s http://localhost:5000/health | jq .entries.postgres.exception # → "Npgsql.SocketException: ***" (ConnectionString masked, not leaked)
# Metrics:
curl -s http://localhost:5000/metrics | grep -E "http_requests_failed_total|job_failed_total|rabbitmq_queue_depth|http_request_duration_ms" | head -n 20
# → http_requests_failed_total{status="403"} 4, http_requests_failed_total{status="500"} 1, job_failed_total{job="document_processing",stage="VirusScan"} 2, rabbitmq_queue_depth{queue="ai.processing.embedding"} 5
# Background job failed metric:
# After DocumentProcessingJob FailedRetryable in 005 SC-004, job_failed_total increments and appears in dashboard
```

---

## Edge cases quick probe

```bash
# Duplicate delivery → one entry (consumer idempotency):
# In test: publish same IntegrationEvent Id twice via IEventBus.PublishAsync same EventId → audit.audit_entries count for that EventId ==1, second HandleAsync returns success without side effect
dotnet test tests/Audit.Tests --filter ConsumerIdempotency -v minimal # → pass (duplicate count ==1)

# Inverted date range → 400:
curl -s "http://localhost:5000/api/audit/entries?from=2026-09-02T00:00:00Z&to=2026-09-01T00:00:00Z" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq . # → 400 Audit.DateRangeInvalid

# Empty range → 200 empty:
curl -s "http://localhost:5000/api/audit/entries?from=2020-01-01T00:00:00Z&to=2020-01-02T00:00:00Z" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '{totalCount, items}' # → totalCount=0, items=[]

# Sensitive masking:
curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq '.items[] | select(.action=="ConfigurationChanged") | .beforeAfterSnapshot' | grep -q '"***"' && echo "masked ok" # → masked

# Missing CorrelationId → generated:
curl -s -X POST http://localhost:5000/api/projects -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" -d '{"name":"No CID project"}' | jq . # → succeeds without X-Correlation-Id header, audit entry has generated Guid CorrelationId (check Response header X-Correlation-Id == audit.CorrelationId)
curl -s -D - http://localhost:5000/api/projects -H "Authorization: Bearer $ALICE_TOKEN" -o /dev/null | grep -i X-Correlation-Id

# Cross-tenant 404 shadow:
curl -s "http://localhost:5000/api/audit/entries?correlationId=$CID" -H "Authorization: Bearer $OTHER_TENANT_TOKEN" | jq . # → 404 (never 403)

# High volume pagination 1k entries:
for i in $(seq 1 100); do curl -s "http://localhost:5000/api/audit/entries?page=$i&pageSize=10" -H "Authorization: Bearer $AUDITOR_ALL_TOKEN" | jq -e '.items | length == 10' > /dev/null || echo "page $i failed"; done # → all pages <300ms p95 via index

# OTel backend down → audit still succeeds:
# Stop Seq/Loki container, then POST /api/documents with CID → audit entry still queryable via SearchAuditEntries within 2s (audit is transactional via outbox, not best-effort OTel)
```

