# Contract: AI Domain → Integration Events

**Module**: `AiProcessing` (BC-06) → consumers: `Audit` (BC-10), `Notifications` (BC-08), `Search` (BC-07 re-index of extracted metadata), `AiProcessing` pipeline self-orchestration | **Transport**: BuildingBlocks Outbox (`IOutboxWriter` + `OutboxProcessor`) → `BuildingBlocks.EventBus.RabbitMQ` (topic exchange `integration_events`, durable, publisher confirms) | **Delivery**: at-least-once — handlers MUST be idempotent (keyed by `EventId` or `(OperationId,Stage,AttemptCount)`)

---

## Domain events (in-process, dispatched in `AiProcessingDbContext.SaveChanges` via `AppDbContextBase`)

| Domain Event | Raised by | Outbox maps to | Persisted where |
|--------------|-----------|---------------|-----------------|
| `LlmOperationQueued {OperationId, DocumentId, DocumentVersionId, OperationType, Model, PromptVersionId, CorrelationId, TenantId}` | `LlmOperation.Create` | `LlmOperationQueuedIntegrationEvent` | `ai_processing.llm_operations` + `outbox_messages` same tx |
| `LlmOperationCompleted {OperationId, ResultId, OperationType}` | `LlmOperation.MarkSucceeded(Validation)` or `ReviewGateHandler` → `MaxAttempts`? | `LlmOperationCompletedIntegrationEvent` | `llm_operations.StageStatusesJson` + `llm_results` same tx |
| `LlmOperationFailed {OperationId, Stage, Reason, Retryable, AttemptCount}` | `LlmOperation.MarkFailed(stage, reason)` | `LlmOperationFailedIntegrationEvent` | same job fields + `LastError` |
| `LlmOperationRetried {OperationId, Stage, AttemptCount}` | `LlmOperation.RetryStage(stage)` | `LlmOperationRetriedIntegrationEvent` | same |
| `PromptVersionPublished {PromptVersionId, OperationType, VersionNumber, PublishedBy}` | `LlmPromptVersion.PublishNewVersion` | `PromptVersionPublishedIntegrationEvent` | `llm_prompt_versions` new row |
| `LlmResultGenerated {ResultId, OperationId, DocumentId, ReviewStatus, Provenance}` | `LlmResult.Create` | `LlmResultGeneratedIntegrationEvent` | `llm_results` new row (provenance NOT NULL) |
| `LlmResultApproved {ResultId, ReviewerId, Rationale}` | `LlmResult.Approve` | `LlmResultApprovedIntegrationEvent` | `llm_results.ReviewStatus=Approved` + `llm_reviews` append |
| `LlmResultRejected {ResultId, ReviewerId, Rationale}` | `LlmResult.Reject` | `LlmResultRejectedIntegrationEvent` | same |
| `LlmResultSuperseded {ResultId, SupersededByResultId}` | `LlmResult.MarkSuperseded(newResultId)` | `LlmResultSupersededIntegrationEvent` | `llm_results.ReviewStatus=Superseded` |
| `LlmReviewCreated {ReviewId, ResultId, ReviewerId, Decision}` | `LlmReview.Create` | `LlmReviewCreatedIntegrationEvent` | `llm_reviews` append |
| `RagQueryExecuted {OperationId, Query, RetrievedCount, FilteredOutCount, CorrelationId}` | `AskDocumentQuestionHandler` | `RagQueryExecutedIntegrationEvent` | same tx as `LlmOperation` QuestionAnswering + `LlmResult` with `ChunkReferences` |
| `LlmProcessingStageCompleted {OperationId, Stage}` | `LlmOperation.MarkSucceeded(stage)` | `LlmProcessingStageCompletedIntegrationEvent` | same |
| `LlmProcessingStageFailed {OperationId, Stage, Reason, Retryable}` | `LlmOperation.MarkFailed` | `LlmProcessingStageFailedIntegrationEvent` | same |

All domain events are dispatched by `AppDbContextBase.SaveChangesAsync` before commit; `IOutboxWriter.StageAsync(integrationEvent)` persists JSON to `outbox_messages` in same DB tx. `OutboxProcessor` polls via `SELECT ... FOR UPDATE SKIP LOCKED` and publishes to RabbitMQ topic `ai.*` with confirms.

---

## Integration events (cross-BC, via RabbitMQ topic `integration_events`)

Each integration event implements `IntegrationEvent { Guid Id; DateTime OccurredOnUtc; }` (BuildingBlocks.EventBus.Abstractions). Topic routing key = `ai.<event>` (topic exchange).

```csharp
// Contracts in AiProcessing.Contracts/Events/
public sealed record LlmOperationQueuedIntegrationEvent(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, int OperationTypeId, string ModelProvider, string ModelName, string PromptVersion, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationCompletedIntegrationEvent(Guid OperationId, Guid ResultId, int OperationTypeId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationFailedIntegrationEvent(Guid OperationId, string Stage, string Reason, bool Retryable, int AttemptCount, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationRetriedIntegrationEvent(Guid OperationId, string Stage, int AttemptCount, Guid CorrelationId) : IntegrationEvent;
public sealed record PromptVersionPublishedIntegrationEvent(Guid PromptVersionId, int OperationTypeId, int VersionNumber, Guid PublishedBy) : IntegrationEvent;
public sealed record LlmResultGeneratedIntegrationEvent(Guid ResultId, Guid OperationId, Guid DocumentId, int OperationTypeId, string ReviewStatus, string ProvenanceJson, Guid TenantId) : IntegrationEvent;
public sealed record LlmResultApprovedIntegrationEvent(Guid ResultId, Guid ReviewerId, string Rationale) : IntegrationEvent;
public sealed record LlmResultRejectedIntegrationEvent(Guid ResultId, Guid ReviewerId, string Rationale) : IntegrationEvent;
public sealed record LlmResultSupersededIntegrationEvent(Guid ResultId, Guid SupersededByResultId) : IntegrationEvent;
public sealed record LlmReviewCreatedIntegrationEvent(Guid ReviewId, Guid ResultId, Guid ReviewerId, string Decision, string Rationale) : IntegrationEvent;
public sealed record RagQueryExecutedIntegrationEvent(Guid OperationId, string Query, int RetrievedCount, int FilteredOutCount, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmProcessingStageCompletedIntegrationEvent(Guid OperationId, string Stage) : IntegrationEvent;
public sealed record LlmProcessingStageFailedIntegrationEvent(Guid OperationId, string Stage, string Reason, bool Retryable) : IntegrationEvent;
public sealed record LlmProcessingStageRequestedIntegrationEvent(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, string Stage, Guid TenantId, Guid CorrelationId) : IntegrationEvent; // internal orchestration
```

**Consumers**:

| Event | Consumer BC | Purpose |
|-------|-------------|---------|
| `LlmOperationQueued/Completed/Failed/Retried` | Audit, Monitoring | append audit trail, OTel trace correlation |
| `PromptVersionPublished` | Audit | prompt lineage audit |
| `LlmResultGenerated/Approved/Rejected/Superseded` | Audit, Notifications, Search | audit + `Notification` for reviewers + Search re-index of extracted metadata (BC-07) |
| `LlmReviewCreated` | Audit | reviewer decision audit |
| `RagQueryExecuted` | Audit, Monitoring | RAG retrieval audit (retrieved vs filtered count) |
| `LlmProcessingStage*` | AiProcessing pipeline handlers themselves | self-orchestration; no external BC |
| All | Observability | OTel traces correlated by `CorrelationId` |

**Idempotency**: every handler keys on `EventId` (`IntegrationEvent.Id`) or `(OperationId,Stage,AttemptCount)` dedup for stage events — duplicate delivery (at-least-once) is detected via `outbox_consumed_events(EventId)` or `(OperationId,Stage)` key.

**Ordering**: stages strictly ordered (Extraction before Normalization etc.) — out-of-order delivery is rejected and re-queued; a stage completing before its predecessor's `Succeeded` returns no-op.

---

## Audit mapping

Each domain event also yields an `AuditEntry` row via `Audit` BC (consumed from same integration event topic `audit.ai.*` if `AuditDbContext` subscribes). Shared `AuditEntry` contract with SPEC-002: `{ AuditId, TenantId, ActorId, Action, ResourceType="LlmOperation|LlmResult|LlmPromptVersion", ResourceId, DetailJson, OccurredOn, CorrelationId }` — append-only, never mutated, per Principle VIII.

## Chunk indexing mapping

`LlmResult` extraction findings that are `Approved` may be consumed by `Search` BC to re-index document metadata: `Search` subscribes to `LlmResultApprovedIntegrationEvent` and upserts via `VectorStore` with the approved `ProposedValue` (but never overwrites authoritative fields without human apply).

