# Contract: Document Processing Jobs & Pipeline

**Module**: `Documents` (BC-05) | **Base path**: `/api/documents` (jobs nested) | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP`, `IEndpoint` per slice.

---

## GET /api/documents/{id}/processing — GetProcessingJob

**Query**: `GetProcessingJobQuery(documentId, versionNumber? = current) : IQuery<Result<ProcessingJobResponse>>`

Returns the resumable job for the requested version (or current version if not specified). Observable state for operators — no half-classified documents leak as `Available`.

```json
// Response 200
{
  "jobId": "guid (DocumentProcessingJobId)",
  "documentId": "guid",
  "documentVersionId": "guid",
  "versionNumber": 2,
  "tenantId": "guid",
  "overallStatus": "FailedRetryable",     // Pending|InProgress|Succeeded|FailedRetryable|FailedPermanent
  "currentStage": "VirusScan",            // ProcessingStage Enumeration
  "stages": {
    "Upload":        { "status":"Succeeded",         "attemptCount":1, "updatedAt":"..." },
    "Validation":    { "status":"Succeeded",         "attemptCount":1, "updatedAt":"..." },
    "VirusScan":     { "status":"FailedRetryable",   "attemptCount":2, "lastError":"Infected", "updatedAt":"..." },
    "Metadata":      { "status":"Pending",           "attemptCount":0 },
    "Classification":{ "status":"Pending",           "attemptCount":0 },
    "Storage":       { "status":"Pending",           "attemptCount":0 },
    "Indexing":      { "status":"Pending",           "attemptCount":0 }
  },
  "attemptCount": 2,                      // attemptCount for current stage (alias)
  "lastError": "Infected",
  "lastErrorStage": "VirusScan",
  "ruleVersion": "v3 | null",
  "createdAt": "...",
  "updatedAt": "...",
  "completedAt": null
}
// Errors: 404 (document/job not found or cross-tenant shadow), 403 via IDocumentAccessPolicy (caller must be authorized for the document — job is not separately accessible)
```

**No-half-classified invariant**: `Document.status` is `Available` only when `Storage==Succeeded && Classification==Succeeded`; clients should read `GET /api/documents/{id}` for `status` and `currentStage` — jobs endpoint is diagnostic.

---

## POST /api/documents/{id}/processing/retry — RetryProcessingStage

**Command**: `RetryProcessingStageCommand(documentId, stage?, versionNumber? = current) : ICommand<Result<ProcessingJobResponse>>`

Retries a failed stage idempotently. `stage` is the `ProcessingStage` to retry — defaults to `lastErrorStage` (or current failed stage). Permissioned via `IDocumentAccessPolicy` + `document.processing.retry`.

```json
// Request
{
  "stage": "VirusScan | null",            // if null → retry last failed stage
  "versionNumber": 2,                     // optional — defaults to current version
  "expectedRowVersion": "base64 | null"   // job concurrency — optional, stale → 409
}
// Response 200
{
  "jobId": "guid",
  "documentId": "guid",
  "stage": "VirusScan",
  "newStatus": "Pending",                 // reset to Pending then InProgress then Succeeded/Failed
  "retryAttempt": 3
}
// Behavior: sets `StageStatuses[stage]=Pending`, increments `AttemptCount`, clears `LastError`, publishes `DocumentProcessingStageRequestedIntegrationEvent(stage)` via outbox; handler re-executes. After `maxAttempts=3` a further failure transitions to `FailedPermanent` and requires operator to re-upload or file support case (no silent drop).
// Errors: 400 (stage unknown or not failed — Succeeded stages return 422 business rule "Already succeeded"), 403 (document.processing.retry denied — audited), 404, 409 concurrency, 422 business rule (job is Succeeded overall — no retry)
```

**Domain**: `DocumentProcessingJob.RetryStage(stage, actor)` → `CheckRule(StageIsRetryableRule)` → `DocumentProcessingStageRequested` via outbox.

---

## ProcessingStage Enumeration (shared with handler pipeline)

| Value | Name | Purpose | Idempotent guard |
|-------|------|---------|------------------|
| 1 | `Upload` | HTTP acceptance — persisted in UploadDocument transaction | Succeeded once |
| 2 | `Validation` | MIME/size/tag/bag + tenant/proj link validation | re-run validates same inputs → same result |
| 3 | `VirusScan` | `ISecurityScanProvider.ScanAsync` — infected/unavailable | re-scan same staged bytes |
| 4 | `Metadata` | Extract/snapshot author/dept/tags/type/dates/source/confidentiality/retention/custom bag | re-extract same content → same snapshot |
| 5 | `Classification` | `IClassificationPolicy.ClassifyAsync` — resolves final Classification + ruleVersion | re-classify at current rule version |
| 6 | `Storage` | `IStorageGateway.PutAsync` + SHA-256 re-hash verify | existence check by ContentHash makes retry idempotent |
| 7 | `Indexing` | Publish `DocumentIndexedIntegrationEvent` for BC-07 Search | duplicate publish is idempotent via event id |

**Status per stage**: `Pending(0)` (queued), `InProgress(1)` (handler running), `Succeeded(2)`, `FailedRetryable(3)` (retryable, `AttemptCount < maxAttempts`), `FailedPermanent(4)` (`AttemptCount >= maxAttempts`).

---

## RabbitMQ topics (Infrastructure — not HTTP but contract for handlers)

- `document.processing.validation` (consumed by `ValidationHandler`)
- `document.processing.virusscan`
- `document.processing.metadata`
- `document.processing.classification`
- `document.processing.storage`
- `document.processing.indexing`

Each message: `DocumentProcessingStageRequestedIntegrationEvent { JobId, DocumentId, DocumentVersionId, Stage, TenantId, CorrelationId }` published via outbox `IOutboxWriter` + `OutboxProcessor` (BuildingBlocks). Handlers ack manually and retry with exponential backoff (`2^attempt * 500ms` capped 30s). Same `CorrelationId` flows through `TenantContext` → OTel trace.

---

## Error mapping (pipeline → HTTP via job status)

- `Validation` failure → job `FailedRetryable(reason=ValidationFailed)` and `DocumentStatus=ProcessingFailed` (not `Available`).
- `VirusScan` `Infected` → `FailedRetryable(reason=Infected)` — file never reaches `Storage`; operators see stage + reason in job.
- `VirusScan` `ScannerUnavailable` → same but retryable.
- `Storage` `HashMismatch` → `FailedRetryable(reason=HashMismatch)`.
- After `maxAttempts` → `FailedPermanent` — `RetryProcessingStage` then returns `422 business rule` unless admin forces reset.

---

## Authorization on job endpoints

Jobs inherit document authorization: `GetProcessingJob` and `RetryProcessingStage` both call `IDocumentAccessPolicy.EvaluateAsync` for the parent `Document` with action `Read`/`ProcessingRetry` respectively. No separate job ACL — owning `Document`'s policy covers all stage reads/retries. Denials append `DocumentAccessDenied` entries exactly as `GetDocument` does.
