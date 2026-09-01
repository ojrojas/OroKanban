# Contract: Domain & Integration Events — Notifications (Consumed + Emitted)

**Module**: `Notifications` (BC-09) | **Bus**: `BuildingBlocks.EventBus.RabbitMQ` `integration_events` topic exchange, durable, publisher confirms, manual ack + exponential retries (at-least-once). **This BC never produces business integration events for work/docs — it is terminal consumer + emits only its own `Notification*` domain events for audit.**

---

## 1. Consumed Integration Events (producers → Notifications dispatcher)

All events inherit `IntegrationEvent` (`Id: Guid`, `OccurredOnUtc: DateTime`). `Notifications` subscribes via `NotificationDispatcher` `IIntegrationEventHandler<T>` adapters.

### Work (Projects) — `Projects.Contracts.Events`

```csharp
WorkItemAssignedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, Guid AssigneeId, Guid AssignerId) : IntegrationEvent
WorkItemStatusChangedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, int FromId, int ToId, Guid ActorId) : IntegrationEvent
  // covers Reassigned/Overdue/Blocked/Completed/ReviewRequested via ToId mapping: Reassigned is second Assigned, Overdue/Blocked/Completed/ReviewRequested via status ToId
WorkItemCreatedIntegrationEvent(Guid WorkItemId, Guid ProjectId, Guid TenantId, int TypeId) : IntegrationEvent // optional for creation notification if policy maps
WorkItemReparentedIntegrationEvent(...) // consumed only if policy maps to notification (future)
DependencyAddedIntegrationEvent(...) // not notification trigger in MVP
```

**Mapping**: `WorkItemAssigned` → `NotificationType.WorkItemAssigned` for `Recipient = AssigneeId`; `WorkItemStatusChanged To=Blocked` → `WorkItemBlocked` for `Recipients = { OwnerId, AssigneeId }` (resolved via `INotificationPolicy.ResolveRecipients` that may need to load work item — stubbed in MVP via event-contained assignee/owner ids; if owner not in event, resolver queries `Projects` read model? chosen: dispatcher uses event fields only — if owner missing from event, notification only to `AssigneeId`).

### Documents — `Documents.Contracts.Events`

```csharp
DocumentUploadedIntegrationEvent(Guid DocumentId, Guid DocumentVersionId, Guid TenantId, Guid OwnerId, Guid? ProjectId, string ContentHash, string Classification, string RuleVersion) : IntegrationEvent
DocumentClassifiedIntegrationEvent(Guid DocumentId, string Classification, string RuleVersion, Guid ActorId) : IntegrationEvent
DocumentApprovedIntegrationEvent(Guid DocumentId, Guid ApproverId, DateTime ApprovedAt) : IntegrationEvent
DocumentIndexedIntegrationEvent(...) // not notification trigger
```

**Mapping**: `DocumentUploaded` → `DocumentUploaded` to `Recipient = Project watchers + Owner`? In MVP resolver uses `OwnerId` (DocumentUploaded) and `ApproverId` is not recipient — notification targets stakeholders per policy config; spec acceptance scenario: `Confidential` document approval → stakeholders receive metadata-only notification. For MVP stakeholders = `OwnerId` + explicit watchers list if event carries them; otherwise single `OwnerId` to satisfy test.

### AI — `AiProcessing.Contracts.Events`

```csharp
LlmOperationQueuedIntegrationEvent(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, int OperationTypeId, string ModelProvider, string ModelName, string PromptVersion, Guid TenantId, Guid CorrelationId) : IntegrationEvent // AiReviewRequested mapping
LlmResultGeneratedIntegrationEvent(Guid ResultId, Guid OperationId, Guid DocumentId, int OperationTypeId, string ReviewStatus, string ProvenanceJson, Guid TenantId) : IntegrationEvent
RagQueryExecutedIntegrationEvent(Guid OperationId, string Query, int RetrievedCount, int FilteredOutCount, Guid TenantId, Guid CorrelationId) : IntegrationEvent
```

**Mapping**: `LlmOperationQueued` where `OperationType` is review-related → `AiReviewRequested` to `Document Owner + Reviewer` ; `LlmResultGenerated` where `ReviewStatus=PendingReview` → `AiReviewRequested` variant.

### Risk/Metrics — `Metrics.Contracts.Events` (when available)

```csharp
RiskIncreasedIntegrationEvent(Guid ProjectId, Guid TenantId, int NewScore, int OldScore, string Reason) : IntegrationEvent
```

**Mapping**: `RiskIncreased` → `RiskIncreased` to `Recipients = Project Members + Tenant auditors per INotificationPolicy` (stubbed to project owner in MVP).

### Dispatcher handling (uniform)

```csharp
// Each per-type handler forwards to NotificationDispatcher
public sealed class WorkItemAssignedHandler(NotificationDispatcher d) : IIntegrationEventHandler<WorkItemAssignedIntegrationEvent>
{
    public async Task HandleAsync(WorkItemAssignedIntegrationEvent evt, CancellationToken ct)
        => await d.HandleAsync(evt, ct); // idempotent per DedupeKey, manual ack on success, retry on transient DbUpdateException not 23505
}
```

Topics: `work.assigned`, `work.status.changed`, `document.uploaded`, `document.approved`, `ai.operation.queued`, `ai.result.generated`, `metrics.risk.increased` (wildcard `notifications.*` also works if producers publish to `notifications.<type>` — but current producer topics are per-BC; dispatcher subscribes to each via `AddSubscription<T, Handler>`; single logical bus `integration_events` routes by routing key — chosen to subscribe to each concrete type regardless of routing key).

Ack behavior: handler returns success → RabbitMQ `BasicAck`; `23505` swallow is success → `Ack`; transient `NpgsqlException` with `IsTransient` true → `Nack` with requeue (exponential backoff via `RabbitMqConsumerService` `retryCount` delay); handler is idempotent on requeue.

---

## 2. Emitted Domain Events (Notifications → outbox → Audit)

Emitted by aggregates inside `NotificationsDbContext.SaveChangesAsync` domain dispatch (`AppDbContextBase`).

```csharp
NotificationCreatedDomainEvent(NotificationId Id, Guid RecipientId, int NotificationTypeId, int ChannelId, Guid SourceEventId, Guid? SourceResourceId, Guid? TenantId, DateTime CreatedAt, Guid? CorrelationId) : IDomainEvent
NotificationReadDomainEvent(NotificationId Id, Guid RecipientId, DateTime ReadAt) : IDomainEvent
PreferencesUpdatedDomainEvent(Guid UserId, Guid TenantId, string PreferencesJsonSnapshot, DateTime UpdatedAt) : IDomainEvent
```

**Outbox mapping**: Each domain event is wrapped to an integration event for audit durability:

```csharp
NotificationCreatedIntegrationEvent(Guid NotificationId, Guid RecipientId, int TypeId, int ChannelId, Guid SourceEventId, DateTime CreatedAt, Guid? CorrelationId) : IntegrationEvent
NotificationReadIntegrationEvent(Guid NotificationId, Guid RecipientId, DateTime ReadAt) : IntegrationEvent
PreferencesUpdatedIntegrationEvent(Guid UserId, Guid TenantId, DateTime UpdatedAt) : IntegrationEvent
```

Staged via `IOutboxWriter.StageAsync(integrationEvent, ct)` inside same `SaveChangesAsync` transaction (outbox pattern). `OutboxProcessor` publishes to `audit.*` topic for `AuditEventConsumer` (007).

---

## 3. Emitted Integration Events (Notifications → other BCs)

**None for business** — notifications is supporting/terminal: it does not publish `WorkItem*`/`Document*` events. It only publishes its own `NotificationCreated`/`NotificationRead` for audit/monitoring if needed. Other BCs never consume notification integrations for business decisions.

---

## 4. Event catalog completeness

| Source BC | Event | NotificationType | Recipients (via INotificationPolicy.ResolveRecipients) | Channel fan-out |
|-----------|-------|------------------|--------------------------------------------------------|----------------|
| Projects | `WorkItemAssigned` | `WorkItemAssigned` | `AssigneeId` | `InApp` always, `Email` if pref+policy |
| Projects | `WorkItemStatusChanged→Blocked` | `WorkItemBlocked` | `OwnerId, AssigneeId` | `InApp` |
| Projects | `WorkItemStatusChanged→Completed` | `WorkItemCompleted` | `OwnerId, AssigneeId` | `InApp` |
| Projects | `WorkItemStatusChanged→ReviewRequested` | `ReviewRequested` | `ReviewerId, OwnerId` | `InApp` |
| Documents | `DocumentUploaded` | `DocumentUploaded` | `OwnerId, ProjectWatchers` | `InApp` |
| Documents | `DocumentClassified` | `DocumentClassified` | `OwnerId` | `InApp` |
| Documents | `DocumentApproved` | `DocumentApproved` | `OwnerId, Stakeholders` | `InApp` |
| AI | `LlmResultGenerated` | `AiReviewRequested` | `DocumentOwner, Reviewer` | `InApp` |
| Metrics | `RiskIncreased` | `RiskIncreased` | `ProjectMembers` | `InApp` mandatory |

Adding a new source event requires only adding one `AddSubscription<T, WorkItemAssignedHandler>`-like registration plus one row in `NotificationType` enum and one case in `INotificationContentPolicy` + one case in `INotificationPolicy.ResolveRecipients` — no producer modification.
