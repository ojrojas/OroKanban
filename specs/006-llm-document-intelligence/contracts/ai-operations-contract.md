# Contract: AI Operations

**Module**: `AiProcessing` (BC-06) | **Base path**: `/api/ai` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP` 400/403/404/409, tenant-aware, `IEndpoint` per slice, pagination envelope `{items, totalCount, page, pageSize}`, correlation via `X-Correlation-Id` header.

---

## POST /api/ai/operations — QueueLlmOperation

**Command**: `QueueLlmOperationCommand : ICommand<Result<QueueOperationResponse>>`

Queues an AI operation over an authorized document version. No LLM call blocks HTTP — outbox only.

```json
// Request
{
  "documentId": "guid",
  "documentVersionId": "guid | null",    // null → current version
  "operationType": "Summarization",       // 12 values: Summarization|Classification|MetadataExtraction|EntityExtraction|TaskExtraction|DeadlineExtraction|RequirementExtraction|RiskDetection|ContentCompleteness|VersionComparison|QuestionAnswering|ProjectContextAnalysis
  "promptVersionId": "guid | null",      // null → resolves to current published for that OperationType (snapshot at queue time)
  "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06", "version": "1" }, // optional → defaults to AI:ModelId
  "input": { "additionalContext": "string | null" } // per-operation input contract extra (e.g., VersionComparison.fromVersionId)
}
// Response 202 Accepted — Location: /api/ai/operations/{operationId}
{
  "operationId": "guid (LlmOperationId)",
  "documentId": "guid",
  "documentVersionId": "guid",
  "operationType": "Summarization",
  "operationStatus": "Queued",
  "correlationId": "guid",
  "promptVersionId": "guid",
  "promptVersionNumber": 2,
  "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06" },
  "stageStatuses": { "Extraction": "Pending", "Embedding": "Pending", "LlmProcessing": "Pending" },
  "tenantId": "guid"
}
// Errors: 400 Validation (unknown operationType, model not allowed, template missing {{content}}, document not IsSafe/Available), 401 unauthenticated, 403 forbidden (ai.operation.queue via IDocumentAccessPolicy), 404 tenant-aware (document not found or cross-tenant shadow), 409 concurrency (RowVersion stale on document version snapshot contention)
// Side effects (transaction): LlmOperation(CorrelationId) + outbox LlmOperationQueuedIntegrationEvent + next-stage event; no IChatClient call in-request; provenance snapshot taken at queue time (model+prompt version snapshotted, not live ref) — SC-002 history fidelity
```

**Domain**: `LlmOperation.Create(documentId, versionId, operationType, modelDescriptor, promptVersionId, actor, tenant)` → `CheckRule(ProvenanceCompleteRule pre-check: source readable && IsSafe)` + `IDocumentAccessPolicy.EvaluateAsync` (Golden Rule A + IsSafe gate) → `LlmOperationQueued` → outbox. Prompt resolution: if `promptVersionId==null`, `MAX(VersionNumber) WHERE OperationType==type AND IsPublished`.

---

## POST /api/ai/operations/{operationId}/retry — RetryLlmOperation

**Command**: `RetryLlmOperationCommand(operationId, expectedRowVersion?) : ICommand<Result<QueueOperationResponse>>`

Idempotent retry of a failed operation. Same `OperationId`, increments `AttemptCount`, no duplicate authoritative result.

```json
// Request
{ "expectedRowVersion": "base64 | null" }
// Response 202 Accepted
{
  "operationId": "guid",
  "newStatus": "Queued",
  "retryAttempt": 2,
  "operationStatus": "Queued",
  "correlationId": "guid (same as original)"
}
// Behavior: sets stage `FailedRetryable → Pending`, increments `AttemptCount`, clears `LastError`, publishes `LlmProcessingStageRequestedIntegrationEvent` via outbox with same CorrelationId; handler re-executes failed stage idempotently; at-least-once dedup via (OperationId, Stage, AttemptCount) or EventId. After maxAttempts=3 → FailedPermanent and retry returns 422 unless admin forces.
// Errors: 400 stage unknown, 403 (ai.operation.retry + original queue auth), 404, 409 concurrency, 422 business rule (Already completed / FailedPermanent without force, stage Already succeeded)
```

**Domain**: `LlmOperation.RetryStage(stage, actor)` → `CheckRule(StageIsRetryableRule)` → `LlmOperationRetried` via outbox.

---

## POST /api/ai/prompts — PublishPromptVersion

**Command**: `PublishPromptVersionCommand : ICommand<Result<PromptVersionResponse>>`

Append-only prompt versioning — changing a prompt creates new row, never mutates.

```json
// Request
{
  "operationType": "Summarization",
  "template": "Summarize the following document in 3 bullets:\n<document_content>\n{{content}}\n</document_content>",
  "expectedRowVersion": "base64 | null" // for concurrency if needed
}
// Response 201 Created — Location: /api/ai/prompts/{promptVersionId}
{
  "promptVersionId": "guid (LlmPromptVersionId)",
  "operationType": "Summarization",
  "versionNumber": 2,
  "previousVersionId": "guid (v1) | null",
  "template": "Summarize ...",
  "isPublished": true,
  "publishedAt": "2026-09-01T12:00:00Z",
  "publishedBy": "guid"
}
// Errors: 400 validation (template 1..20k, must contain {{content}}, unknown operationType), 403 (ai.prompt.publish), 409 concurrency, 422 business rule (Attempt to mutate published version — use publish new version)
// Immutability: loading published v1 and attempting Template update via repository returns Error.BusinessRule PromptIsImmutableOncePublishedRule and reload equals original.
```

**Domain**: `LlmPromptVersion.PublishNewVersion(operationType, template, actor)` → new `LlmPromptVersion` with `VersionNumber = max+1`, `IsPublished=true`, `PromptVersionPublished` via outbox.

---

## POST /api/ai/reviews — RequestLlmReview

**Command**: `RequestLlmReviewCommand(resultId, rationale?) : ICommand<Result<LlmReviewResponse>>`

Creates a review request for a result that is `Generated` but `IReviewPolicy` says it should be `PendingReview` — or for manual review.

```json
// Request
{ "resultId": "guid (LlmResultId)", "rationale": "Needs human check | null" }
// Response 202
{ "reviewId": "guid", "resultId": "guid", "reviewStatus": "PendingReview" }
// Errors: 403 (ai.review.request + reviewer can read source document via IDocumentAccessPolicy), 404, 422 business rule (Already Approved/Rejected/Superseded), 409 concurrency
```

**Domain**: `LlmResult.RequestReview(actor)` → transitions `Generated→PendingReview` if `IReviewPolicy.RequiresReview==true` else no-op (already review-required at generation).

---

## POST /api/ai/results/{resultId}/approve — ApproveLlmResult

**Command**: `ApproveLlmResultCommand(resultId, rationale, expectedRowVersion?) : ICommand<Result<LlmResultResponse>>`

```json
// Request
{ "rationale": "Verified — deadline matches source p.12", "expectedRowVersion": "base64 | null" }
// Response 200
{ "resultId": "guid", "reviewStatus": "Approved", "reviewedAt": "2026-09-01T12:10:00Z", "reviewerId": "guid", "rationale": "..." }
// Errors: 403 (ai.review.approve + reviewer can read source document + IReviewPolicy), 422 business rule (illegal transition Generated→Approved without PendingReview unless policy says Generated is approvable, but spec says Generated stays Generated when no review required — Approve only valid for PendingReview; Approved→Approved etc. rejected), 404, 409 concurrency (RowVersion stale)
```

**Domain**: `LlmResult.Approve(reviewer, rationale)` → `CheckRule(ReviewStatusTransitionRule(From=PendingReview, To=Approved))` → `LlmResultApproved` + `LlmReview` append via same-tx outbox. Approved proposals available via explicit `ApplyProposed*` but never silently overwrite authoritative fields.

---

## POST /api/ai/results/{resultId}/reject — RejectLlmResult

**Command**: `RejectLlmResultCommand(resultId, rationale, expectedRowVersion?) : ICommand<Result<LlmResultResponse>>`

```json
// Request
{ "rationale": "Hallucinated entity 'ACME' not in source" }
// Response 200
{ "resultId": "guid", "reviewStatus": "Rejected", "reviewedAt": "...", "reviewerId": "guid" }
// Errors: same as approve (403/422/409)
```

**Domain**: `LlmResult.Reject(reviewer, rationale)` → `PendingReview→Rejected` via `ReviewStatusTransitionRule`; `LlmResultRejected` via outbox.

---

## GET /api/ai/operations/{operationId} — GetOperationProvenance

**Query**: `GetOperationProvenanceQuery(operationId) : IQuery<Result<OperationProvenanceResponse>>`

```json
// Response 200
{
  "operationId": "guid",
  "tenantId": "guid",
  "documentId": "guid",
  "documentVersionId": "guid",
  "operationType": "Summarization",
  "operationStatus": "Completed",
  "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06", "version": "1" },
  "promptVersionId": "guid",
  "promptVersionNumber": 2,
  "correlationId": "guid",
  "stageStatuses": { "Extraction": "Succeeded", "Embedding": "Succeeded", "LlmProcessing": "Succeeded", "Validation": "Succeeded" },
  "attemptCount": 1,
  "lastError": null,
  "createdAt": "2026-09-01T10:00:00Z",
  "createdBy": "guid",
  "provenance": {
    "sourceDocumentId": "guid",
    "sourceDocumentVersionId": "guid",
    "operationId": "guid",
    "operationType": "Summarization",
    "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06" },
    "promptVersion": "v2",
    "createdAt": "2026-09-01T10:00:01Z",
    "createdBy": "guid",
    "processingStatus": "Completed",
    "qualityIndicator": { "confidence": 0.92, "isInjectionFlagged": false, "chunkCount": 3, "tokenCount": 450 }
  }
}
// Errors: 404 tenant-aware, 403 via IDocumentAccessPolicy (must be able to read source document to see its AI provenance per Principle XV)
```

---

## Cross-cutting concerns (all endpoints)

- **Tenant isolation**: every handler reads `TenantContext.TenantId` from JWT `tenant_id`; all EF queries via `LlmOperationByTenantSpec`; cross-tenant → 404 shadow (never 403) — audited.
- **Concurrency**: `RowVersion` base64 `If-Match` / `expectedRowVersion`; stale → 409 `Error.Concurrency`.
- **Validation**: each command has `Validator<T>` (BuildingBlocks ValidationBehavior) covering operationType enum 1..12, model provider allow-list (`openai|azure|ollama|inmemory`), template contains `{{content}}`, document IsSafe/Available check via `IDocumentAccessPolicy`.
- **Auditing**: every success/deny/mutation emits domain event → outbox → `IntegrationEvent` consumed by `Audit` BC (topic `audit.ai.*`), correlated by `CorrelationId`.
- **OTel**: `AddServiceDefaults()` flow; handlers traced with `operationId`/`documentId`/`stage` baggage; `CorrelationId` propagates tenant/OTel trace.
- **Rate limiting**: `QueueLlmOperation` per actor/tenant via `Api` middleware (existing from 002) plus token budget via `Microsoft.ML.Tokenizers` pre-count (reject if exceeds `AI:TokenBudget`).
