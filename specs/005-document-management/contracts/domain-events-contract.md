# Contract: Document Domain → Integration Events

**Module**: `Documents` (BC-05) → consumers: `Search` (BC-07), `Audit` (BC-10), `Notifications` (BC-08), `AiProcessing` (BC-08) | **Transport**: BuildingBlocks Outbox (`IOutboxWriter` + `OutboxProcessor`) → `BuildingBlocks.EventBus.RabbitMQ` (topic exchange `integration_events`, durable, publisher confirms) | **Delivery**: at-least-once — handlers MUST be idempotent (keyed by `EventId`)

---

## Domain events (in-process, dispatched in `DocumentsDbContext.SaveChanges` via `AppDbContextBase`)

| Domain Event | Raised by | Outbox maps to | Persisted where |
|--------------|-----------|---------------|-----------------|
| `DocumentUploaded {DocumentId, DocumentVersionId, TenantId, OwnerId, ProjectId, ContentHash, Name, Classification, RuleVersion}` | `Document.Create` | `DocumentUploadedIntegrationEvent` | `documents.documents` + `document_versions` + `document_processing_jobs` same tx |
| `DocumentVersionPublished {DocumentVersionId, DocumentId, VersionNumber, ContentHash, PublishedBy, RuleVersion}` | `Document.PublishNewVersion` | `DocumentVersionPublishedIntegrationEvent` | new `document_versions` row |
| `DocumentVersionSuperseded {DocumentVersionId, SupersededByVersionId}` | same as above (prior version) | same integration event `SupersededByVersionId` field | prior version row unchanged (append) |
| `DocumentClassified {DocumentId, Classification, RuleVersion, ActorId}` | `Document.Reclassify` or Classification pipeline stage | `DocumentClassifiedIntegrationEvent` | `documents.documents.Classification*` updated |
| `DocumentAccessed {DocumentId, ActorId, Classification, RuleVersion, Action=Read|Download, Granted=true}` | `IDocumentAccessPolicy` grant path (handler) | `DocumentAccessedIntegrationEvent` (+ `AuditEntry` via Audit BC) | `document_access_entries` append |
| `DocumentAccessDenied {DocumentId, ActorId, Reason, Classification, RuleVersion, Action}` | `IDocumentAccessPolicy` deny path | `DocumentAccessDeniedIntegrationEvent` + `AuditEntry` | `document_access_entries` append (`granted=false`) |
| `DocumentDeleted {DocumentId, ActorId, DeletedAt}` | `Document.Delete` | `DocumentDeletedIntegrationEvent` | `documents.documents.Status=Deleted` |
| `DocumentApproved {DocumentId, ApproverId, ApprovedAt, FromStatus, ToStatus}` | `Document.Approve` | `DocumentApprovedIntegrationEvent` | `documents.documents.Status=Approved` |
| `DocumentProcessingStageCompleted {JobId, Stage}` | `DocumentProcessingJob.MarkSucceeded(stage)` | `DocumentProcessingStageCompletedIntegrationEvent` | `document_processing_jobs.StageStatusesJson` |
| `DocumentProcessingFailed {JobId, Stage, Reason, Retryable, AttemptCount}` | `DocumentProcessingJob.MarkFailed(stage, reason, retryable)` | `DocumentProcessingFailedIntegrationEvent` | same job fields + `LastError` |

All domain events are dispatched by `AppDbContextBase.SaveChangesAsync` (BuildingBlocks) before commit; `IOutboxWriter.StageAsync(integrationEvent)` persists the serialized JSON to `outbox_messages` in the same DB transaction. `OutboxProcessor` (hosted service from `BuildingBlocks.Kernel.Infrastructure`) polls/releases via `SELECT ... FOR UPDATE SKIP LOCKED` and publishes to RabbitMQ topic `document.*` with confirms.

---

## Integration events (cross-BC, via RabbitMQ topic `integration_events`)

Each integration event implements `IntegrationEvent { Guid EventId; DateTime OccurredOn; string CorrelationId; }` (BuildingBlocks.EventBus). Topic routing key = `document.<event>` (topic exchange).

```csharp
// Example contracts in Documents.Contracts/Events/
public sealed record DocumentUploadedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid DocumentVersionId,
    Guid TenantId, Guid OwnerId, Guid? ProjectId, string ContentHash, string Classification, string RuleVersion) : IntegrationEvent;

public sealed record DocumentVersionPublishedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid DocumentVersionId, int VersionNumber, string ContentHash, Guid PublishedBy, string RuleVersion, Guid? SupersededVersionId) : IntegrationEvent;

public sealed record DocumentClassifiedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, string Classification, string RuleVersion, Guid ActorId) : IntegrationEvent;

public sealed record DocumentAccessedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid ActorId, string Classification, string RuleVersion, string Action) : IntegrationEvent;

public sealed record DocumentAccessDeniedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid ActorId, string Reason, string Classification, string RuleVersion, string Action) : IntegrationEvent;

public sealed record DocumentDeletedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid ActorId, DateTime DeletedAt, Guid TenantId) : IntegrationEvent;

public sealed record DocumentApprovedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid ApproverId, DateTime ApprovedAt) : IntegrationEvent;

public sealed record DocumentIndexedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid DocumentId, Guid DocumentVersionId, string ContentHash, string MimeType, string MetadataSnapshotJson, string Classification, string RuleVersion, Guid TenantId) : IntegrationEvent;
    // Emitted by Indexing stage on success — consumed by BC-07 Search for indexing; includes MetadataSnapshot for authorization-filtered search

public sealed record DocumentProcessingStageCompletedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid JobId, Guid DocumentId, string Stage) : IntegrationEvent;

public sealed record DocumentProcessingFailedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid JobId, Guid DocumentId, string Stage, string Reason, bool Retryable, int AttemptCount) : IntegrationEvent;

public sealed record DocumentProcessingStageRequestedIntegrationEvent(
    Guid EventId, DateTime OccurredOn, Guid JobId, Guid DocumentId, Guid DocumentVersionId, string Stage, Guid TenantId) : IntegrationEvent;
    // Internal orchestration event — produced by outbox/handlers to drive next stage (topic document.processing.<stage>)
```

**Consumers**:

| Event | Consumer BC | Purpose |
|-------|-------------|---------|
| `DocumentUploaded` / `DocumentVersionPublished` | Audit | append audit trail |
| `DocumentClassified` | Audit, Search | audit + search re-index with new classification |
| `DocumentAccessed` / `DocumentAccessDenied` | Audit | `audit.authorization.denied` + access metrics |
| `DocumentDeleted` / `DocumentApproved` | Audit, Notifications | audit + `Notification` for stakeholders |
| `DocumentIndexed` | Search (BC-07) | build/update search index; stores `Classification` + `TenantId` for authorization-filtered search |
| `DocumentProcessingStageCompleted/Failed/StageRequested` | Documents pipeline handlers themselves | self-orchestration; no external BC |
| All | Monitoring/Observability | OTel traces correlated by `CorrelationId` |

**Idempotency**: every handler keys on `EventId` (`IntegrationEvent.EventId`) — duplicate delivery (at-least-once) is detected via `outbox_consumed_events(EventId)` or `(JobId,Stage)` dedup for processing-stage events.

**Ordering**: stages are strictly ordered (Validation before VirusScan, etc.) — out-of-order delivery is rejected and re-queued; a stage completing before its predecessor's `Succeeded` returns a no-op.

---

## Audit mapping

Each domain event also yields an `AuditEntry` row via `Audit` BC (consumed from same integration event topic `audit.document.*` if `AuditDbContext` subscribes). The `AuditEntry` contract is shared with SPEC-002: `{ AuditId, TenantId, ActorId, Action, ResourceType="Document", ResourceId, DetailJson, OccurredOn, CorrelationId }` — append-only, never mutated, per Principle VIII.
