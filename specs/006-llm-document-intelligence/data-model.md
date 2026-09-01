# Data Model: LLM and Document Intelligence

**Feature**: 006-llm-document-intelligence | **Date**: 2026-09-01 | **Schema**: `ai_processing` (`AiProcessingDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("ai_processing")` + `ApplyConfiguration(new OutboxEntityTypeConfiguration())`)

## Entities

### 1. LlmOperation (AggregateRoot, BC-06, `ai_processing.llm_operations`)

Root identity for an AI processing request; tracks per-stage resumable status. Mirrors `DocumentProcessingJob` pattern from SPEC-005 but distinct aggregate.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `LlmOperationId : StronglyTypedId<Guid>` | PK, `Guid.NewGuid()` on `QueueLlmOperation` | Root identifier; `CorrelationId` == `Id` for OTel |
| `TenantId` | `Guid` | required, from `TenantContext` | Isolation — every query/spec includes it; cross-tenant → 404 |
| `DocumentId` | `DocumentId` | required, FK logical to `documents.documents` | Logical FK — validated via `IDocumentAccessPolicy` (tenant match + readable + IsSafe) |
| `DocumentVersionId` | `DocumentVersionId` | required | Snapshot — version that was current at queue time |
| `OperationTypeId` | `int` | FK `OperationType` Enumeration, required | e.g., `Summarization=1`, `QuestionAnswering=11` |
| `OperationStatusId` | `int` | FK `OperationStatus` Enumeration, required | Lifecycle — see transition map |
| `ModelProvider` | `string` | 1–100, required | `ModelDescriptor.Provider` e.g., `openai`, `azure`, `ollama` |
| `ModelName` | `string` | 1–200, required | e.g., `gpt-4o-2024-08-06` (pinned) |
| `ModelVersion` | `string` | 1–100, required | `ModelDescriptor.Version` |
| `PromptVersionId` | `LlmPromptVersionId` | FK, required | Snapshot — `PublishPromptVersion` current at queue time |
| `CorrelationId` | `Guid` | required, default `Id` | Propagates via `TenantContext` → OTel baggage `operationId/stage` |
| `StageStatusesJson` | `jsonb` | required | Map `AiProcessingStage → StageStatus` + `AttemptCount` + `LastError` (see job section) |
| `OverallStatus` | `int` | `Pending|InProgress|Succeeded|FailedRetryable|FailedPermanent` derived | `Succeeded` only when `Validation==Succeeded` + (`Review==Succeeded` or `Generated` path) |
| `AttemptCount` | `int` | `>=0`, default 0 | Max per stage |
| `LastError` | `string?` | max 1k | Last failure reason (e.g., `ProviderUnavailable`, `QuotaExceeded`, `InjectionFlagged`) |
| `LastErrorStage` | `int?` | FK `AiProcessingStage`, nullable | Stage that last failed |
| `CreatedAt` | `DateTime` | UTC, required | Queued at |
| `UpdatedAt` | `DateTime` | UTC | Updated on each stage transition |
| `CompletedAt` | `DateTime?` | UTC, set when `OverallStatus==Succeeded` | |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Optimistic concurrency — `RetryLlmOperation` race → 409 |

**Status lifecycle** (`OperationStatus : Enumeration`):

```
Queued(1) → Processing(2) → Completed(3)
Processing → FailedRetryable(4) (transient)
FailedRetryable → Processing (on retry)
FailedRetryable → FailedPermanent(5) (after maxAttempts 3)
Completed → Superseded(6) (when newer result supersedes)
Any → Cancelled(7) (explicit cancellation)
```

**Stage order & behavior** (mirrors SPEC-005 but AI-specific):

| Stage Enum | Value | Handler | Success next | Failure → `FailedRetryable` → maxAttempts=3 → `FailedPermanent` |
|-----------|-------|---------|--------------|-------------------------------------------------------------------|
| `Extraction` | 1 | `ExtractionHandler` via `IChatClient` (prompt `extract {{content}}`) | `Normalization` | `FailedRetryable(reason=ProviderUnavailable|ExtractionFailed)` |
| `Normalization` | 2 | `NormalizationHandler` (normalize JSON/text, sanitize injection) | `Classification` | `FailedRetryable(reason=NormalizationFailed)` |
| `Classification` | 3 | `ClassificationHandler` via `IDocumentClassifier` (LLM classify) | `Chunking` | `FailedRetryable(reason=ClassificationFailed)` |
| `Chunking` | 4 | `ChunkingHandler` via `DataIngestion.TextChunker` (512/50) | `Indexing` | `FailedRetryable(reason=ChunkingFailed)` — only if IsSafe+Available, else skips |
| `Indexing` | 5 | `IndexingHandler` (upsert chunk metadata to PG + VectorStore) | `Embedding` | `FailedRetryable(reason=IndexingFailed)` |
| `Embedding` | 6 | `EmbeddingHandler` via `IEmbeddingGenerator` + VectorStore | `LlmProcessing` | `FailedRetryable(reason=EmbeddingFailed|QuotaExceeded)` |
| `LlmProcessing` | 7 | `LlmProcessingHandler` via `IChatClient` (operation-type prompt `{{content}}` boundary, temp 0, retry 3) | `Validation` | `FailedRetryable(reason=LlmFailed)` |
| `Validation` | 8 | `ValidationHandler` via `IResultValidationPolicy` (schema + injection flag) | `HumanReview` or terminal `Succeeded` (if no review required) | `FailedRetryable(reason=ValidationFailed|InjectionFlagged)` |
| `HumanReview` | 9 | `ReviewGateHandler` (check `IReviewPolicy` → set `PendingReview` vs `Generated`) | terminal `Succeeded` | N/A (review decision via `Approve/Reject`) |

**Events (domain → outbox → integration)**: `LlmOperationQueued {OperationId, DocumentId, OperationType, Model, PromptVersionId, CorrelationId}`, `LlmOperationCompleted {OperationId, ResultId}`, `LlmOperationFailed {OperationId, Stage, Reason, Retryable}`, `LlmOperationRetried {OperationId, AttemptCount}`.

**Indexes**: `INDEX (TenantId, DocumentId)`, `INDEX (TenantId, OperationTypeId)`, `INDEX (TenantId, OverallStatus)`.

### 2. LlmPromptVersion (AggregateRoot, BC-06, `ai_processing.llm_prompt_versions`)

Immutable once `IsPublished=true`. Append-only; new template creates new row with `VersionNumber` monotonic per `OperationType`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `LlmPromptVersionId : StronglyTypedId<Guid>` | PK | |
| `OperationTypeId` | `int` | required, FK `OperationType` | Per-type versioning |
| `VersionNumber` | `int` | required, `>=1`, `UNIQUE (OperationTypeId, VersionNumber)` | Monotonic 1..n per type |
| `Template` | `string` | required, 1–20k, must contain `{{content}}` | Structured boundary — validated via `TemplateContainsContentPlaceholderRule` |
| `IsPublished` | `bool` | required, default true | Guard for `PromptIsImmutableOncePublishedRule` |
| `PublishedAt` | `DateTime` | UTC, required | Snapshot |
| `PublishedBy` | `Guid` | required | Actor who published |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency on creation path only |

**Invariants**: `IsPublished==true` → all setters throw `PromptIsImmutableOncePublishedRule` (no template update after publish); `Template` must contain `{{content}}`; `VersionNumber` is max+1 per OperationType.

**Events**: `PromptVersionPublished {PromptVersionId, OperationTypeId, VersionNumber, PublishedBy}`.

**Indexes**: `UNIQUE (OperationTypeId, VersionNumber)`, `INDEX (OperationTypeId, IsPublished)`.

### 3. LlmResult (AggregateRoot, BC-06, `ai_processing.llm_results`)

Append-only per operation; one current `LlmResult` per `LlmOperation` (retry updates same result, not duplicate). Carries mandatory `Provenance`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `LlmResultId : StronglyTypedId<Guid>` | PK | |
| `TenantId` | `Guid` | required | Isolation |
| `DocumentId` | `Guid` | required | Source document |
| `DocumentVersionId` | `Guid` | required | Snapshot version |
| `OperationId` | `LlmOperationId` | FK `llm_operations`, required, indexed | Parent operation — `UNIQUE (OperationId)` for current (retry idempotency) |
| `OperationTypeId` | `int` | required | Denormalized from operation |
| `ProvenanceJson` | `jsonb` | required, `NOT NULL`, Npgsql jsonb | `Provenance` VO serialized (mandatory — never null) |
| `Content` | `string/jsonb` | required, 1..50k | Answer/summary/extraction JSON |
| `ProposedValueJson` | `jsonb` | nullable | AI proposal when targeting authoritative field (deadline/task/requirement) — never overwrites authoritative |
| `ChunkReferencesJson` | `jsonb` | nullable | `IReadOnlyList<ChunkReference>` for RAG — sources |
| `ReviewStatusId` | `int` | FK `ReviewStatus` Enumeration, required | Lifecycle — see transition map |
| `QualityIndicatorJson` | `jsonb` | nullable | `QualityIndicator` VO (`confidence`, `isInjectionFlagged`, `chunkCount`, `tokenCount`) |
| `SupersededBy` | `LlmResultId?` | nullable, FK `llm_results` | When `Superseded` |
| `CreatedAt` | `DateTime` | UTC, required | Generation time |
| `CreatedBy` | `Guid` | required | Actor (`sub`) or `System` |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency for approve/reject race → 409 |

**ReviewStatus lifecycle** (`ReviewStatus : Enumeration`):

```
Generated(1) → PendingReview(2) → Approved(3) | Rejected(4)
PendingReview|Approved → Superseded(5) (when newer version supersedes)
Any → Failed(6) (terminal on validation failure)
Generated stays Generated if IReviewPolicy.RequiresReview==false
* → ReviewStatus transition illegal → Error.BusinessRule("Transition not allowed: {from}→{to}")
```

**Invariants**: `Provenance` is mandatory — constructor validates completeness (`SourceDocumentId`, `SourceDocumentVersionId`, `OperationId`, `OperationType`, `Model`, `PromptVersion`, `CreatedAt/By` non-default); missing → `Error.Validation("Provenance.Required")`.

**Events**: `LlmResultGenerated {ResultId, OperationId, ReviewStatus}`, `LlmResultApproved {ResultId, ReviewerId}`, `LlmResultRejected {ResultId, ReviewerId, Reason}`, `LlmResultSuperseded {ResultId, SupersededByResultId}`.

**Indexes**: `UNIQUE (OperationId)` (one current per operation, retry idempotency), `INDEX (TenantId, DocumentId)`, `INDEX (TenantId, ReviewStatusId)` for `ListPendingReviews`, `INDEX (TenantId, OperationTypeId)`.

### 4. LlmReview (AggregateRoot/Entity, BC-06, `ai_processing.llm_reviews`) — append-only review decision

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `LlmReviewId : StronglyTypedId<Guid>` | PK | |
| `ResultId` | `LlmResultId` | FK `llm_results`, required, indexed | Parent result — `UNIQUE (ResultId)` when not superseded (one review per result) |
| `ReviewerId` | `Guid` | required | `sub` who decided |
| `TenantId` | `Guid` | required | Isolation |
| `Decision` | `int` | `Approved(1)|Rejected(2)` | `ReviewStatus` decision |
| `Rationale` | `string` | 1–2000, required | Required rationale per R8 |
| `ReviewedAt` | `DateTime` | UTC, required | Decision time |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency guard |

**Events**: `LlmReviewCreated {ReviewId, ResultId, ReviewerId, Decision}` → transitions parent `LlmResult.ReviewStatus`.

### 5. ChunkReference (ValueObject, BC-06, stored as jsonb in `llm_results.chunk_references` + separate `ai_processing.chunk_references` table for query)

For RAG source tracking and `VectorStoreRecord` payload filtering.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentId` | `Guid` | required | Source document |
| `DocumentVersionId` | `Guid` | required | Snapshot version |
| `ChunkId` | `int` | `>=0`, required | Deterministic 0..n per document version (DataIngestion order) |
| `TenantId` | `Guid` | required | Isolation — pre-filter key |
| `ClassificationValue` | `string` | 1–100, required | Classification at indexing time — pre-filter |
| `ProjectId` | `Guid?` | nullable | For project membership pre-filter |
| `IsSafe` | `bool` | required | Must be true for retrieval — filter predicate |
| `IsCurrentVersion` | `bool` | required | True for current version's chunks only |
| `Score` | `float?` | nullable, 0..1 | Similarity score after retrieval — set at query time |
| `Text` | `string` | 1..10k, stored in VectorStore, not PG | Chunk text (metadata in PG is length only) |

**Table** `ai_processing.chunk_references` (for filtering test):

| Field | Type | Notes |
|-------|------|-------|
| `Id` | `Guid` PK | |
| `TenantId` | `Guid` indexed | |
| `DocumentId` | `Guid` indexed | |
| `DocumentVersionId` | `Guid` | |
| `ChunkId` | `int` | |
| `ClassificationValue` | `string` indexed | |
| `ProjectId` | `Guid?` | |
| `IsSafe` | `bool` indexed | |
| `IsCurrentVersion` | `bool` indexed | |
| `EmbeddingId` | `string` | Key into VectorStore |

### 6. ReviewPolicy (Entity, `ai_processing.review_policies`) — versioned per tenant

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `TenantId` | `Guid` | required, indexed | Scope |
| `OperationTypeId` | `int` | required | `OperationType` Enumeration |
| `ClassificationValue` | `string` | 1–100, required | e.g., `Confidential` |
| `RequiresReview` | `bool` | required | True = `PendingReview` gate |
| `EffectiveFrom` | `DateTime` | UTC, required | When this policy became current |
| `IsCurrent` | `bool` | required | Exactly one `true` per `(TenantId, OperationTypeId, ClassificationValue)` |

**Seed**: For dev, seed `deadlineExtraction × Confidential → true`, `summarization × Public → false` as examples; all other combos default `true` when not found (safe default per FR-010).

## Value Objects & Enumerations (Domain invariants — validate at construction)

### OperationType (Enumeration) — 12 values

| Id | Name | Input Contract | Review via `IReviewPolicy` |
|----|------|----------------|----------------------------|
| 1 | `Summarization` | `DocumentVersionId` → summary text | via policy |
| 2 | `Classification` | `DocumentVersionId` → label | via policy |
| 3 | `MetadataExtraction` | `DocumentVersionId` → metadata JSON | via policy |
| 4 | `EntityExtraction` | `DocumentVersionId` → entities list | via policy |
| 5 | `TaskExtraction` | `DocumentVersionId` → tasks list | `true` (targets WorkItem authoritative) |
| 6 | `DeadlineExtraction` | `DocumentVersionId` → deadline | `true` |
| 7 | `RequirementExtraction` | `DocumentVersionId` → requirements | `true` |
| 8 | `RiskDetection` | `DocumentVersionId` → risks | via policy |
| 9 | `ContentCompleteness` | `DocumentVersionId` → completeness score | via policy |
| 10 | `VersionComparison` | `(FromVersionId, ToVersionId)` → diff | via policy |
| 11 | `QuestionAnswering` (RAG) | `Query` + scope → answer + Sources | via policy (RAG) |
| 12 | `ProjectContextAnalysis` | `DocumentVersionId` + `ProjectId` → analysis | via policy |

### OperationStatus (Enumeration) — for LlmOperation

`Queued(1)`, `Processing(2)`, `Completed(3)`, `FailedRetryable(4)`, `FailedPermanent(5)`, `Superseded(6)`, `Cancelled(7)`.

### ReviewStatus (Enumeration) — for LlmResult

`Generated(1)`, `PendingReview(2)`, `Approved(3)`, `Rejected(4)`, `Superseded(5)`, `Failed(6)`.

### Provenance (VO) — mandatory on every LlmResult

- `SourceDocumentId: Guid` (required), `SourceDocumentVersionId: Guid` (required), `OperationId: Guid` (required), `OperationType: string/int` (required), `Model: ModelDescriptor` (required), `PromptVersion: string` (required, e.g., `v2`), `CreatedAt: DateTime` UTC (required), `CreatedBy: Guid` (`sub` or `System` GUID, required), `ProcessingStatus: string` (`Completed|Failed`), `QualityIndicator: QualityIndicator?` (optional). All fields validated at construction via `ProvenanceCompleteRule`.

### ModelDescriptor (VO)

- `Provider: string` 1–50 (e.g., `openai`, `azure`, `ollama`, `inmemory`), `ModelName: string` 1–200 (e.g., `gpt-4o-2024-08-06`), `Version: string` 1–100. Value-equality.

### QualityIndicator (VO)

- `Confidence: float?` 0..1, `QualityScore: float?`, `IsInjectionFlagged: bool` default false, `ChunkCount: int?`, `TokenCount: int?`, `RelevanceScore: float?`.

### ChunkReference (VO) — see entity table above, value-equality over DocumentId+VersionId+ChunkId+TenantId.

## Relationships

- `LlmOperation 1 — 1 LlmResult` via `OperationId` (retry idempotency: `UNIQUE (OperationId)` on current result)
- `LlmPromptVersion 1 — * LlmOperation` via `PromptVersionId` (snapshot FK, not cascade)
- `LlmResult 1 — 0..1 LlmReview` via `ResultId`
- `LlmResult 1 — * ChunkReference` via `ChunkReferencesJson` (jsonb) + `chunk_references` table for filtering
- Logical FK: `LlmOperation.DocumentId → documents.documents.Id` (read-only check via `IDocumentAccessPolicy`, no EF FK cross-schema)
- `LlmOperation.DocumentVersionId → documents.document_versions.Id` (same)

## Cross-module contracts consumed

- `IDocumentAccessPolicy` (from `Documents.Contracts`): `EvaluateAsync(actor, tenant, document)` for `QueueLlmOperation` gate + `IAuthorizedRetrievalPolicy` pre-filter
- `IManagementHierarchy` (from `Organization.Contracts`): `IsInSubtree(ancestorId, descendantId)` for authorized retrieval OR branch
- `IProjectMembership` (from `Projects.Contracts` via Organization): `IsMember(projectId, userId)` — same
- `TenantContext` (from Api): `tenant_id` from JWT — first predicate in every Spec
