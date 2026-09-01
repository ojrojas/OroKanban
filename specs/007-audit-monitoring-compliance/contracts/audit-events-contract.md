# Contract: Audit Event Catalog and Emission Path

**Module**: `Audit` (BC-08) producer = all BCs via `AuditEventConsumer`; **Transport**: `BuildingBlocks.EventBus.RabbitMQ` topic `audit.*` + `integration_events` (durable, publisher confirms, manual ack) | **Delivery**: at-least-once — consumer MUST be idempotent (keyed by `EventId` dedup `audit_consumed_events`)

---

## Event catalog R2 → AuditAction mapping (31 actions, each 1:1 to at least one `DomainEvent→IntegrationEvent`)

| AuditAction (`AuditAction` Enumeration) | Trigger domain event (example producer BC) | Integration event type (from `BuildingBlocks.EventBus.Abstractions.IntegrationEvent` with `EventId` + `CorrelationId`) | AuditEntry example |
|------------------------------------------|---------------------------------------------|-------------------------------------------------------------------------------------------------------------------|-------------------|
| `AuthenticationSucceeded(1)` | `UserLoggedInDomainEvent` (Identity) | `UserLoggedInIntegrationEvent(ActorId, TenantId, CorrelationId)` | `Actor=User`, `ResourceType=Authentication`, `Result=Success` |
| `AuthenticationFailed(2)` | `UserLoginFailedDomainEvent` | `UserLoginFailedIntegrationEvent(AttemptedIdentity, TenantId, CorrelationId)` | `Actor=Anonymous`, `Result=Failed` |
| `AuthorizationDenied(3)` | `DocumentAccessDeniedDomainEvent` (Documents) | `DocumentAccessDeniedIntegrationEvent(DocumentId, ActorId, CorrelationId)` | `Actor=denied actor`, `ResourceId=documentId`, `Result=Denied` |
| `ProjectCreated(4)` | `ProjectCreatedDomainEvent` (Projects) | `ProjectCreatedIntegrationEvent(ProjectId, TenantId, CorrelationId)` | `Actor=creator`, `ResourceId=projectId` |
| `ProjectUpdated(5)` | `ProjectUpdatedDomainEvent` | `ProjectUpdatedIntegrationEvent(ProjectId, Before, After, CorrelationId)` | `BeforeAfterSnapshot` with `Before: name=Old → After: name=New` (masked) |
| `WorkItemCreated(6)` | `WorkItemCreatedDomainEvent` | `WorkItemCreatedIntegrationEvent(WorkItemId, ProjectId, TenantId, CorrelationId)` | `ResourceType=WorkItem` |
| `WorkItemUpdated(7)` | `WorkItemUpdatedDomainEvent` | `WorkItemUpdatedIntegrationEvent(WorkItemId, Before, After)` | `Before/After` masked |
| `WorkItemAssigned(8)` | `WorkItemAssignedDomainEvent` | `WorkItemAssignedIntegrationEvent(WorkItemId, AssigneeId)` | `Actor=assigner` |
| `WorkItemStatusChanged(9)` | `WorkItemStatusChangedDomainEvent` | `WorkItemStatusChangedIntegrationEvent(WorkItemId, From, To)` | `BeforeAfter: status Backlog→In Progress` |
| `ProjectMetricChanged(10)` | `MetricCalculatedDomainEvent` (Metrics) | `MetricCalculatedIntegrationEvent(ProjectId, MetricId)` | `ResourceType=ProjectMetric` |
| `DocumentUploaded(11)` | `DocumentUploadedDomainEvent` | `DocumentUploadedIntegrationEvent(DocumentId, TenantId, CorrelationId)` | `ResourceId=documentId` |
| `DocumentClassified(12)` | `DocumentClassifiedDomainEvent` | `DocumentClassifiedIntegrationEvent(DocumentId, Classification, CorrelationId)` | `Before/After: Public→Confidential` |
| `DocumentVersionPublished(13)` | `DocumentVersionPublishedDomainEvent` | `DocumentVersionPublishedIntegrationEvent(DocumentId, VersionNumber)` | `ResourceId=documentId/versionNumber` |
| `DocumentAccessed(14)` | `DocumentAccessedDomainEvent` | `DocumentAccessedIntegrationEvent(DocumentId, ActorId, CorrelationId)` | `Result=Success` |
| `DocumentAccessDenied(15)` | `DocumentAccessDeniedDomainEvent` | `DocumentAccessDeniedIntegrationEvent(DocumentId, ActorId)` | `Result=Denied` |
| `DocumentDeleted(16)` | `DocumentDeletedDomainEvent` | `DocumentDeletedIntegrationEvent(DocumentId, CorrelationId)` | `Result=Success` |
| `DocumentApproved(17)` | `DocumentApprovedDomainEvent` | `DocumentApprovedIntegrationEvent(DocumentId, ApproverId, CorrelationId)` | `Actor=approver` |
| `PermissionChanged(18)` | `PermissionUpdatedDomainEvent` (Identity) | `PermissionUpdatedIntegrationEvent(OrganizationId, RoleId)` | `ResourceId=organizationId` |
| `GrantAdded(19)` | `ExplicitGrantAddedDomainEvent` | `ExplicitGrantAddedIntegrationEvent(DocumentId, GranteeId)` | `ResourceId=documentId` |
| `GrantRevoked(20)` | `ExplicitGrantRevokedDomainEvent` | `ExplicitGrantRevokedIntegrationEvent(DocumentId, GranteeId)` | `Result=Success` |
| `HierarchyChanged(21)` | `HierarchyChangedDomainEvent` (Organization) | `HierarchyChangedIntegrationEvent(OrganizationUnitId, BeforeParent, AfterParent)` | `Before/After` masked |
| `LlmOperationQueued(22)` | `LlmOperationQueuedDomainEvent` (AiProcessing) | `LlmOperationQueuedIntegrationEvent(OperationId, DocumentId, CorrelationId)` | `ResourceId=operationId` |
| `LlmOperationCompleted(23)` | `LlmOperationCompletedDomainEvent` | `LlmOperationCompletedIntegrationEvent(OperationId, ResultId, CorrelationId)` | `Result=Success` |
| `LlmOperationFailed(24)` | `LlmOperationFailedDomainEvent` | `LlmOperationFailedIntegrationEvent(OperationId, Stage, Reason, CorrelationId)` | `Result=Failed` |
| `LlmResultGenerated(25)` | `LlmResultGeneratedDomainEvent` | `LlmResultGeneratedIntegrationEvent(ResultId, OperationId, CorrelationId)` | `ResourceId=resultId` |
| `LlmResultApproved(26)` | `LlmResultApprovedDomainEvent` | `LlmResultApprovedIntegrationEvent(ResultId, ReviewerId, CorrelationId)` | `Actor=reviewer` |
| `LlmResultRejected(27)` | `LlmResultRejectedDomainEvent` | `LlmResultRejectedIntegrationEvent(ResultId, ReviewerId)` | `Result=Success` (rejection is success for audit) |
| `LlmReviewCreated(28)` | `LlmReviewCreatedDomainEvent` | `LlmReviewCreatedIntegrationEvent(ReviewId, ResultId, CorrelationId)` | `ResourceId=reviewId` |
| `RagQueryExecuted(29)` | `RagQueryExecutedDomainEvent` | `RagQueryExecutedIntegrationEvent(OperationId, RetrievedCount, FilteredOutCount, CorrelationId)` | `Result=Success` + `DetailJson: {retrievedCount, filteredOutCount}` |
| `ConfigurationChanged(30)` | `ConfigurationUpdatedDomainEvent` (any BC) | `ConfigurationUpdatedIntegrationEvent(ConfigurationKey, Before, After)` | `Before/After` masked (`ApiKey→***`) |
| `AuditCorrected(31)` | (audit correction itself, not business) | (consumer-internal — `AuditEntry` with `Action=AuditCorrected` is produced by `POST /api/audit/corrections` or by `AuditEntry` correction flow) | `ResourceId=correctedAuditId` |

**Topic**: `audit.<action>` (e.g., `audit.document.approved`) on `RabbitMQ` `topic` exchange `audit_events` (durable, `x-queue-type=quorum`), publisher confirms `true`, consumer `IIntegrationEventHandler<T>` generic per type funneled to `AuditEventConsumer.HandleAsync(IntegrationEvent, CancellationToken)`.

---

## Emission path (R3) — domain→outbox→integration→audit consumer idempotent

```text
Business DbContext (Documents/Organization/AiProcessing)
  │ SaveChangesAsync → collects DomainEvents (IAggregateRoot.DomainEvents) → IOutboxWriter.StageAsync(IntegrationEvent) in same transaction (outbox_messages row with EventId, Payload JSON, CorrelationId)
  │
  ▼
OutboxProcessor (hosted service from BuildingBlocks.Kernel.Infrastructure, polling SELECT ... FOR UPDATE SKIP LOCKED FROM outbox_messages WHERE ProcessedOn IS NULL LIMIT 10)
  │ publish via IEventBus.PublishAsync(IntegrationEvent) with publisher confirms (retry 3, exponential)
  │
  ▼
RabbitMQ topic exchange integration_events (audit.* wildcard)
  │
  ▼
AuditEventConsumer (BackgroundService in Audit.Infrastructure, IIntegrationEventHandler<IntegrationEvent> generic, manual ack + exponential retries, at-least-once)
  │ 1. SELECT 1 FROM audit.audit_consumed_events WHERE EventId=@event.Id FOR UPDATE → if exists, ACK and return (duplicate)
  │ 2. INSERT INTO audit.audit_consumed_events (EventId, ProcessedAt, Action, CorrelationId) VALUES (@event.Id, now, @event.Action, @event.CorrelationId) in AuditDbContext transaction
  │ 3. IAuditMaskingPolicy.Mask(BeforeAfterSnapshot) → masked BeforeJson/AfterJson
  │ 4. INSERT INTO audit.audit_entries (...) VALUES (...) with (AuditId=newGuid(), Timestamp=UtcNow, Action=mapped AuditAction, Actor=event.ActorId, CorrelationId=event.CorrelationId ?? Activity.Baggage, BeforeJson=masked, PreviousHash=computeIfChaining else null, Hash=computeIfChaining else null) same transaction as step 2
  │ 5. AuditDbContext.SaveChangesAsync (atomic dedup + entry)
  │ 6. ACK RabbitMQ (manual ack after SaveChanges success)
  │
  ▼
Queryable via SearchAuditEntries/GetAuditTrail/GetOperationTimeline (authorization-filtered)
```

**Idempotency**: `EventId` is `IntegrationEvent.Id` (`Guid.NewGuid()` at `IntegrationEvent` construction, stable per `IOutboxWriter.StageAsync` serialization). `audit_consumed_events(EventId PK UNIQUE)` dedup handles duplicate delivery (same `EventId` twice → second `INSERT` gets `UniqueConstraintViolation` → caught, `Transaction.Rollback`, `ACK` without second `AuditEntry`). `AuditEntry` itself has `AuditId=Guid.NewGuid()` (different namespace) — one `EventId` maps to one `AuditEntry` (not `AuditId==EventId`). `AuditEntry` `CorrelationId` is not dedup key — same `CorrelationId` legitimately appears on many entries (7-entry workflow), so dedup only on `EventId`.

**CorrelationId propagation**: Every `IntegrationEvent` concrete record has `Guid CorrelationId` property (added to each `*IntegrationEvent` in `src/BuildingBlocks/BuildingBlocks.EventBus/IntegrationEvent.cs` or via subtype `CorrelationId` param — e.g., `DocumentApprovedIntegrationEvent(DocumentId, ApproverId, Guid CorrelationId) : IntegrationEvent` where base `IntegrationEvent` has `Id`+`OccurredOnUtc` and we add `CorrelationId` as extra). `CorrelationIdMiddleware` at `Api` `Program.cs` does `if (!Headers.TryGetValue("X-Correlation-Id", out var cid)) cid=Guid.NewGuid().ToString(); Activity.Current?.SetBaggage("CorrelationId",cid); TenantContext.CorrelationId=Guid.Parse(cid); Response.Headers["X-Correlation-Id"]=cid;` — this `TenantContext.CorrelationId` is read by `IOutboxWriter` and set on `IntegrationEvent.CorrelationId` and `AuditEntry.CorrelationId`.

---

## AuditEntry construction (consumer side, masked, append-only)

```csharp
var maskedSnapshot = auditMaskingPolicy.Mask(new BeforeAfterSnapshot(beforeJson, afterJson)); // ApiKey→***
var entry = new AuditEntry(
    auditId: AuditEntryId.New(),
    timestamp: DateTime.UtcNow,
    actor: new ActorReference(@event.ActorId, ActorType.User, displayNameMasked),
    action: AuditAction.FromIntegrationEventType(@event.GetType()), // 1:1 map per table above
    resourceType: ResourceType.FromIntegrationEvent(@event), // e.g., "Document" for DocumentApproved
    resourceId: @event.ResourceId.ToString(),
    organizationId: @event.OrganizationId, // snapshot from @event
    tenantId: @event.TenantId,
    result: AuditResult.FromIntegrationEventResult(@event), // Success/Denied/Failed + ErrorCode
    correlationId: @event.CorrelationId ?? TenantContext.CorrelationId ?? Activity.Baggage["CorrelationId"],
    clientMetadata: new ClientMetadata(HttpContext?.Connection.RemoteIpAddress?.ToString(), HttpContext?.Request.Headers.UserAgent),
    beforeAfterSnapshot: maskedSnapshot,
    previousHash: hashChainingEnabled ? ComputePreviousHash(@event.TenantId) : null, // SELECT tail Hash FOR UPDATE
    hash: hashChainingEnabled ? ComputeHash(previousHash, auditId, timestamp, action, actorId) : null
);
await auditRepository.AddAsync(entry); // only AddAsync, no Update/Remove — compile-time impossibility
await auditUnitOfWork.SaveChangesAsync(ct); // atomic with audit_consumed_events INSERT
```

**BeforeAfterSnapshot masking**: `IAuditMaskingPolicy.Mask` depth-first `JsonDocument` traversal: for each `JsonProperty` where `property.Name` equals any `maskedField` in `Audit:MaskedFields` (`ApiKey,Password,Secret,ConnectionString,Token,CreditCard,PrivateKey` default), replace `Value` with `"***"` (string kind). Raw `beforeJson` never reaches `audit_entries`.

**Tamper-evidence**: `PreviousHash` = `SELECT Hash FROM audit_entries WHERE TenantId=@t ORDER BY Timestamp DESC LIMIT 1 FOR UPDATE` (tail lock to serialize concurrent inserts for same tenant). `Hash = SHA256(PreviousHash + "|" + AuditId + "|" + Timestamp.ToString("O") + "|" + Action + "|" + ActorId)` (UTF8, lower hex 64). `VerifyChain()` recomputes. If `ADR-007-01` chooses `NoChaining`, `PreviousHash`/`Hash` stay `NULL` and `DB REVOKE UPDATE, DELETE ON audit.audit_entries FOR app_orokanban` is applied in migration.

