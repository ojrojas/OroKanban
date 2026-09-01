# Quickstart: Document Management Validation

**Feature**: 005-document-management | **Date**: 2026-09-01 | **Depends on**: 002-identity-access-organization (`IManagementHierarchy`, `IAuthorizationEvaluator`, `TenantContext`) + 003-projects-work-kanban (`Project`, `ProjectMember`, `WorkItem`, `RowVersion`) must be complete first; `DocumentsDbContext` schema `documents` migrations + S3-compatible storage (MinIO dev) + scan stub required.

## Prerequisites

- 003 passed: `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `ProjectsDbContext` + `OrganizationDbContext` migrations applied, `IManagementHierarchy` + `TenantContext` (002) reachable.
- `oroidentityserver` running (Authority via `Identity__Authority`); JWTs carry `sub` + `tenant_id` + `roles`.
- App running: `aspire run` or `dotnet run --project src/Api/Api.csproj` — `/health` <1s, Aspire dashboard shows `postgres`, `redis`, `rabbitmq`, `objectstorage` (MinIO) resources.
- This feature's migrations applied (once, after 002/003):

```bash
dotnet ef migrations add Documents_005_Initial --project src/Modules/Documents/Documents.Infrastructure --startup-project src/Api/Api.csproj --context DocumentsDbContext
dotnet ef database update --project src/Modules/Documents/Documents.Infrastructure --startup-project src/Api/Api.csproj --context DocumentsDbContext

# Tests
dotnet test tests/Documents.Tests -v minimal           # new: Unit (VersionImmutability, ClassificationPolicy incl extensions, AccessPolicyMatrix, Lifecycle, MetadataSnapshotEquality, ProcessingStage) + Integration (Pipeline with MinIO+scan stub, Retry, Dedup, HashVerification) + Security (classification×actor matrix per SPEC-013)
dotnet test tests/Architecture -v minimal
npm --prefix src/Web test -- --include="**/documents.store.spec.ts" # document SignalStore withRequestStatus
```

---

## Setup — seed project, roles, and classification baseline

```bash
# Tokens with distinct sub/tenant_id/roles/clearances
# Owner/Alice (owns doc), Bob (not in subtree/not member/no grant), Manager Carol (in owner's subtree), Auditor Dave (document.audit.read), Admin Eve (document.approve)
ALICE_TOKEN=<jwt sub=Alice tenant_id=T roles=[uploader]>
BOB_TOKEN=<jwt sub=Bob tenant_id=T roles=[viewer]>
CAROL_TOKEN=<jwt sub=Carol tenant_id=T roles=[manager]>   # Carol is Alice's manager via Organization hierarchy
AUDITOR_TOKEN=<jwt sub=Dave tenant_id=T roles=[auditor]>
APPROVER_TOKEN=<jwt sub=Eve tenant_id=T roles=[approver]>  # allowed up to Restricted
OTHER_TENANT_TOKEN=<jwt sub=Zed tenant_id=T2 roles=[viewer]>

# Project owned by Alice, no Bob membership
PROJECT_ID=$(curl -s -X POST http://localhost:5000/api/projects \
  -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Docs Revamp","managerId":"'"$(jq -r .sub <(echo $ALICE_TOKEN | cut -d. -f2 | base64 -d))"'","status":"Active","priority":"High","criticality":"High"}' | jq -r .id)

# Link check: Bob is not member (no POST /api/projects/$PROJECT_ID/members for Bob)
# Carol is manager of Alice via PUT /api/organization/management-relationships (SPEC-002) — gives subtree

# Default classifications seeded: Public|Internal|Confidential|Restricted|HighlyRestricted → allowed levels via GET /api/documents/classifications?organizationId=...
curl -s http://localhost:5000/api/documents/classifications -H "Authorization: Bearer $ALICE_TOKEN" | jq .levels # → 5 defaults
```

---

## Verify — the 8 acceptance pillars (spec SC-001..008)

### SC-001 — Upload acceptance <500ms with outbox, storage by hash, no pipeline in-request

```bash
time curl -s -X POST http://localhost:5000/api/documents \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -F "file=@contract.pdf;type=application/pdf" \
  -F "name=contract.pdf" \
  -F "projectId=$PROJECT_ID" \
  -F "classificationHint=Confidential" \
  -F "author=Jane Doe" \
  -F "tags=finance" -F "tags=q3" \
  -F "effectiveDate=2026-09-01T00:00:00Z" \
  -F "retentionDays=365" \
  | tee /tmp/upload.json | jq '{documentId, versionNumber, contentHash, status, currentStage}'

DOC_ID=$(jq -r .documentId /tmp/upload.json)
# → versionNumber 1, contentHash=64hex, status Uploaded, currentStage Validation
# duration from `time` → <500ms (no VirusScan/Classification in-request)

# Metadata only in DB — no blob bytes
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $ALICE_TOKEN" | jq '{currentVersionNumber, contentHash, mimeType, size, status, currentStage}'
# DocumentProcessingJob observable
curl -s http://localhost:5000/api/documents/$DOC_ID/processing -H "Authorization: Bearer $ALICE_TOKEN" | jq '{overallStatus, currentStage, stages: .stages.VirusScan}'
# Storage — MinIO bucket contains blob keyed by hash (via mc or S3 list; not via DB)
# After ~seconds, Classification+Storage complete → status eventually Available
for i in 1 2 3 4 5; do curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $ALICE_TOKEN" | jq -r .status; sleep 1; done
# → Available (when pipeline completes; virus stub clean)
```

### SC-002 — Version immutability: correction creates v2, v1 unchanged

```bash
# Publish correction with new bytes
curl -s -X POST http://localhost:5000/api/documents/$DOC_ID/versions \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -F "file=@contract.v2.pdf;type=application/pdf" \
  -F "name=contract.v2.pdf" \
  -F "author=Jane Doe v2" \
  | jq '{versionNumber, contentHash}'

# v1 still retrievable unchanged
curl -s http://localhost:5000/api/documents/$DOC_ID/versions -H "Authorization: Bearer $ALICE_TOKEN" | jq '.items[] | {versionNumber, contentHash, publishedBy}'
# → two entries, v1 hash == original /tmp/upload.json contentHash, v2 different hash
# Direct mutation rejected (attempt to update v1 via repository — unit proves domain guard)
```

### SC-003 — Access denied and audited, no binary served (classification × actor matrix)

```bash
# Upload Confidential doc (ALICE_TOKEN) already done — $DOC_ID is Confidential
# Bob outside subtree/membership/grants → denied
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $BOB_TOKEN" | jq .
# → 404 shadow (or 403 → mapped to 404), no downloadUrl
curl -s http://localhost:5000/api/documents/$DOC_ID/download -H "Authorization: Bearer $BOB_TOKEN" -i | head
# → 404, body empty, no bytes

# Denial audited — auditor sees it
curl -s "http://localhost:5000/api/documents/$DOC_ID/history?action=Denied" -H "Authorization: Bearer $AUDITOR_TOKEN" | jq '.items[] | {actorId, action, reason, classification}'
# → {action:Denied, reason:NotInSubtreeOrMembership|InsufficientClassification, classification:Confidential}

# Authorized owner/manager → granted
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $ALICE_TOKEN" | jq .downloadUrl # → presigned URL when Available
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $CAROL_TOKEN" | jq .downloadUrl # → same (subtree grants even though not project member)
```

### SC-004 — Virus-scan failure explicit, retryable, no half-classified

```bash
# Upload with name that triggers stub infected (FakeScanProvider name-contains "virus")
curl -s -X POST http://localhost:5000/api/documents \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -F "file=@virus.pdf;type=application/pdf" \
  -F "name=virus.pdf" \
  | tee /tmp/virus.json | jq .
VIRUS_DOC=$(jq -r .documentId /tmp/virus.json)

# Job shows VirusScan FailedRetryable, status never Available
curl -s http://localhost:5000/api/documents/$VIRUS_DOC/processing -H "Authorization: Bearer $ALICE_TOKEN" | jq '{overallStatus, currentStage, lastError, lastErrorStage}'
# → overallStatus FailedRetryable, currentStage VirusScan, lastError Infected, lastErrorStage VirusScan
curl -s http://localhost:5000/api/documents/$VIRUS_DOC -H "Authorization: Bearer $ALICE_TOKEN" | jq .status
# → ProcessingFailed (never Available/Indexed)

# Retry with clean bytes (re-upload clean version or retry stage if scan now clean)
curl -s -X POST http://localhost:5000/api/documents/$VIRUS_DOC/processing/retry \
  -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" \
  -d '{"stage":"VirusScan"}' | jq .

# After retry with clean stub → Succeeded and advances Classification→Storage→Indexing
sleep 3
curl -s http://localhost:5000/api/documents/$VIRUS_DOC/processing -H "Authorization: Bearer $ALICE_TOKEN" | jq '{overallStatus, stages}'
```

### SC-005 — Classification rule version recorded (v3 vs v4)

```bash
# Rules at v3 at time of first upload (DOC_ID ruleVersion v3)
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $ALICE_TOKEN" | jq .ruleVersion # → v3

# Advance rules to v4 (via PUT /api/documents/classifications/rules — org extension or default)
curl -s -X PUT http://localhost:5000/api/documents/classifications/rules \
  -H "Authorization: Bearer $APPROVER_TOKEN" -H "Content-Type: application/json" \
  -d '{"organizationId":"<orgId>","ruleSetJson":{"orgExtensions":[{"level":"TopSecretFinance","value":101}]}}' | jq .version # → v4

# New doc after v4
curl -s -X POST http://localhost:5000/api/documents \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -F "file=@contract2.pdf;type=application/pdf" -F "name=contract2.pdf" \
  | jq .ruleVersion # → v4

# Old doc still v3 (auditor view)
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $AUDITOR_TOKEN" | jq .ruleVersion # → v3 (unchanged)
curl -s http://localhost:5000/api/documents/$DOC_ID/versions -H "Authorization: Bearer $AUDITOR_TOKEN" | jq '.items[].ruleVersion' # → v3 for v1/v2
```

### SC-006 — Auditor access history returns reads, denials, downloads

```bash
# Perform reads/denials/downloads on DOC_ID (already done: ALICE read, BOB denied×2, ALICE download)
curl -s http://localhost:5000/api/documents/$DOC_ID/download -H "Authorization: Bearer $ALICE_TOKEN" -o /tmp/dl.pdf

# Auditor query
curl -s "http://localhost:5000/api/documents/$DOC_ID/history?page=1&pageSize=50" \
  -H "Authorization: Bearer $AUDITOR_TOKEN" | jq '{totalCount, items:.items[] | {action, granted, classification}}'
# → Reads + Denieds + Downloads chronologically, each with actor, action, timestamp, classification

# Non-auditor/non-owner denied for history
curl -s http://localhost:5000/api/documents/$DOC_ID/history -H "Authorization: Bearer $BOB_TOKEN" | jq .
# → 403 Forbidden
```

### SC-007 — Approval gates with lifecycle guard

```bash
# Move doc to PendingApproval (Classification stage does → Available; approver workflow sets PendingApproval explicitly or via policy)
# For quickstart: assume doc is Available after pipeline — approve needs PendingApproval
# Transition via status change (admin endpoint or re-classify to PendingApproval)
# Simulate: set status PendingApproval via workflow (or via ClassifyDocument reason)
# Approve
curl -s -X POST http://localhost:5000/api/documents/$DOC_ID/approve \
  -H "Authorization: Bearer $APPROVER_TOKEN" -H "Content-Type: application/json" \
  -d '{}' | jq '{status}'
# → Approved

# Illegal transition rejected
curl -s -X POST http://localhost:5000/api/documents/$DOC_ID/approve \
  -H "Authorization: Bearer $APPROVER_TOKEN" -H "Content-Type: application/json" \
  -d '{}' | jq .
# → 422 Error.BusinessRule "Transition not allowed: Approved→Approved"
```

### SC-008 — Metadata-only DB, object storage by hash, hash verification

```bash
# DB has no blob column — verify schema
# (in psql or via migration diff) — only ContentHash in documents table
psql $DATABASE_URL -c "\d documents.documents" | grep -i bytea # → no rows

# Hash mismatch detection: re-upload with same metadata but tampered bytes staged
# (integration test HashVerificationTests covers this — storage stage recomputes SHA-256 and fails with FailedRetryable reason HashMismatch)
dotnet test tests/Documents.Tests -k HashVerification -v minimal # → pass
```

---

## Edge cases quick probe

```bash
# Cross-tenant → 404 shadow, audited
curl -s http://localhost:5000/api/documents/$DOC_ID -H "Authorization: Bearer $OTHER_TENANT_TOKEN" | jq . # → 404

# Custom bag validation — key >64 → 400
curl -s -X POST http://localhost:5000/api/documents \
  -H "Authorization: Bearer $ALICE_TOKEN" \
  -F "file=@a.pdf;type=application/pdf" -F "name=a.pdf" \
  -F 'customMetadata={"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa":"v"}' | jq . # → 400 validation

# Dedup — upload identical bytes as different document → same ContentHash, one blob in MinIO
HASH1=$(jq -r .contentHash /tmp/upload.json)
HASH2=$(curl -s -X POST http://localhost:5000/api/documents -H "Authorization: Bearer $ALICE_TOKEN" -F "file=@contract.pdf;type=application/pdf" -F "name=contract-dup.pdf" | jq -r .contentHash)
echo "$HASH1 $HASH2" # equal; MinIO object list has one key sha256/$HASH1.pdf
```
