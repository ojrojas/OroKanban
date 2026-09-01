# Data Model: Notifications and Collaboration

**Feature**: 008-notifications-collaboration | **Date**: 2026-09-01 | **Schema**: `notifications` (`NotificationsDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("notifications")` + `ApplyConfiguration(new OutboxEntityTypeConfiguration())`)

## Entities

### 1. Notification (AggregateRoot, BC-09, `notifications.notifications`)

Per-recipient per-channel inbox item derived from a single integration event. One row = one delivery to one channel. `InApp` row is the inbox; `Email` row is the future channel.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `NotificationId : StronglyTypedId<Guid>` | PK, `Guid.NewGuid()` on creation | Root identifier |
| `RecipientId` | `Guid` | required, indexed composite `(RecipientId, CreatedAt desc)` | Owner — every query filters by it first |
| `TenantId` | `Guid?` | nullable, indexed | Isolation when producer carries `TenantId`; if null, row is still scoped via `RecipientId` lookup |
| `SourceEventId` | `Guid` | required, part of `UNIQUE(SourceEventId, RecipientId, Channel)` | `IntegrationEvent.Id` — dedupe key component |
| `SourceResourceId` | `Guid?` | nullable | Domain resource id (e.g., `WorkItemId`, `DocumentId`) for link builder |
| `SourceResourceType` | `string?` | 1..100 nullable | e.g., `WorkItem`, `Document`, `LlmResult` — for deep link routing |
| `NotificationTypeId` | `int` | FK `NotificationType` Enumeration, required | Maps 1:1 to integration event type |
| `ChannelId` | `int` | FK `Channel` Enumeration, required, part of unique | `InApp=1`, `Email=2` |
| `DeliveryStateId` | `int` | FK `DeliveryState` Enumeration, required | `Pending|Delivered|Failed|SkippedByPreference|SkippedByPolicy` |
| `Title` | `string` | required, 1..200, content-safety allowlist | Safe metadata only, e.g., `Work item "Sprint-12" assigned to you` |
| `Body` | `string` | required, 1..2000, content-safety allowlist | Safe metadata + link text, never document body/AI payload |
| `Link` | `string` | required, 1..500 | Deep link `/projects/{p}/work-items/{id}` etc., authorization enforced at navigation |
| `CreatedAt` | `DateTime` | UTC, required, indexed desc | Creation time — ordering key |
| `ReadAt` | `DateTime?` | UTC nullable | Set on `MarkRead`, idempotent |
| `CorrelationId` | `Guid?` | nullable, from OTel baggage | Propagation `X-Correlation-Id → TenantContext → Activity → IntegrationEvent → Notification` |
| `RowVersion` | `byte[]?` | `IsRowVersion()` optional | Optimistic concurrency if `Title`/`DeliveryState` mutated (read path does not need it; mutable only via `MarkRead`) |

**Invariants** (`CheckRule` in Domain): `DedupeKeyRequiredRule` (`SourceEventId != Guid.Empty && RecipientId != Guid.Empty && ChannelId valid`), `TitleRequiredRule`, `LinkRequiredRule`, `ContentSafetyRule` (per-type `Title`/`Body` must not contain body/payload — enforced via `INotificationContentPolicy` allowlist, not regex). `CreatedAt` never updated after creation. `ReadAt` monotonic — once set cannot be cleared; second `MarkRead` is no-op idempotent (no duplicate `NotificationRead`).

**Events (domain → outbox)**: `NotificationCreated {NotificationId, RecipientId, NotificationType, Channel, SourceEventId, CorrelationId}`, `NotificationRead {NotificationId, RecipientId, ReadAt}`.

**Indexes**: `UNIQUE(SourceEventId, RecipientId, Channel)` (dedupe), `INDEX(RecipientId, CreatedAt desc)` + filtered `WHERE ReadAt IS NULL` for `GetUnreadCount`, `INDEX(TenantId, CreatedAt desc)` if tenant-scoped, `INDEX(CorrelationId)` for tracing.

**Behavior**: `Notification.Create(recipientId, sourceEventId, type, channel, content, link, tenantId, correlationId)` raises `NotificationCreated`; `MarkRead()` sets `ReadAt=UtcNow` if null and raises `NotificationRead`, else no-op (idempotent).

### 2. NotificationPreference (AggregateRoot, BC-09, `notifications.notification_preferences`)

Per-user matrix `NotificationType × Channel → enabled`. Mutable with optimistic concurrency.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `UserId : Guid` | PK, also `UserId` — one row per user | Root is user |
| `TenantId` | `Guid` | required, from `TenantContext` | Tenant that owns preference |
| `PreferencesJson` | `jsonb` | required, `NOT NULL`, default `{}` | `Dictionary<int,Dictionary<int,bool>>` outer `NotificationTypeId` → inner `ChannelId→enabled` |
| `UpdatedAt` | `DateTime` | UTC, required | Last update |
| `RowVersion` | `byte[]` | `IsRowVersion()` / `IsConcurrencyToken()` required | Optimistic concurrency — concurrent `UpdatePreferences` → 409 `Error.Conflict` |

**Invariants**: Outer keys must be valid `NotificationType` ids (1..100), inner keys valid `Channel` ids — validated by `UpdatePreferencesValidator` (unknown → 400, no partial update). Values are bool. Missing entry means unset → defaults to provider (see policy). `PreferencesJson` never stores mandated types as disabled for persistence? Allowed but ignored at read-time merge (policy overrides). `PreferencesUpdated` emitted after successful `SaveChanges`.

**Events**: `PreferencesUpdated {UserId, TenantId, UpdatedAt, PreferencesJsonSnapshot}`.

**Indexes**: `UNIQUE(UserId)` (PK), `INDEX(TenantId)`.

### 3. NotificationDedupe conceptual (no separate table — unique on `notifications`)

Dedupe is enforced by `UNIQUE(SourceEventId, RecipientId, Channel)` on `notifications` itself (Decision 2). No `notification_consumed_events` table. Duplicate `INSERT` → `23505` → swallow as already-delivered. Observability via log `Duplicate notification deduped {SourceEventId} {RecipientId} {Channel}`.

If future cross-channel dedupe audit needs explicit table, it can be introduced behind `INotificationDeduplicationService` without changing `Notification` aggregate.

## Value Objects & Enumerations (Domain invariants — validate at construction)

### NotificationType (Enumeration) — 11 seeded + extensible

| Id | Name | Source IntegrationEvent | Title template (safe) | Link template |
|----|------|--------------------------|-----------------------|---------------|
| 1 | `WorkItemAssigned` | `WorkItemAssignedIntegrationEvent` | `You were assigned work item "{WorkItemId}"` | `/projects/{ProjectId}/work-items/{WorkItemId}` |
| 2 | `WorkItemReassigned` | `WorkItemReassigned` (or second `Assigned` with different assignee) | `Work item "{WorkItemId}" reassigned` | `/projects/{ProjectId}/work-items/{WorkItemId}` |
| 3 | `WorkItemOverdue` | `WorkItemStatusChanged` / scheduler `Overdue` | `Work item "{WorkItemId}" is overdue` | `/projects/{ProjectId}/work-items/{WorkItemId}` |
| 4 | `WorkItemBlocked` | `WorkItemStatusChanged To=Blocked` | `Work item "{WorkItemId}" blocked` | `/projects/{ProjectId}/work-items/{WorkItemId}` |
| 5 | `WorkItemCompleted` | `WorkItemStatusChanged To=Completed` | `Work item "{WorkItemId}" completed` | `/projects/{ProjectId}/work-items/{WorkItemId}` |
| 6 | `ReviewRequested` | `WorkItemStatusChanged To=InReview` / `ReviewRequested` | `Review requested for "{WorkItemId}"` | `/projects/{ProjectId}/work-items/{WorkItemId}/review` |
| 7 | `DocumentUploaded` | `DocumentUploadedIntegrationEvent` | `Document "{DocumentName}" uploaded` | `/documents/{DocumentId}` |
| 8 | `DocumentClassified` | `DocumentClassifiedIntegrationEvent` | `Document "{DocumentId}" classified {Classification}` | `/documents/{DocumentId}` |
| 9 | `DocumentApproved` | `DocumentApprovedIntegrationEvent` | `Document "{DocumentId}" approved` | `/documents/{DocumentId}` |
| 10 | `AiReviewRequested` | `LlmResultGeneratedIntegrationEvent` / `LlmOperationQueued` | `AI review requested for "{DocumentId}"` | `/documents/{DocumentId}/ai-results/{ResultId}` |
| 11 | `RiskIncreased` | `RiskIncreasedIntegrationEvent` (Metrics) | `Risk increased for project "{ProjectId}"` | `/projects/{ProjectId}/risks` |

Extensibility: adding `Id=12 DocumentProcessingFailed` etc. requires no migration — `Enumeration` is code, `NotificationTypeId` is int FK via `HasConversion`.

### Channel (Enumeration)

`InApp(1)` — default, guaranteed, inbox row `Delivered`. `Email(2)` — future, stub logs + `DeliveryState` row. Extensible `Push(3)`, `Webhook(4)` via new enum value + `IChannel` impl.

### DeliveryState (Enumeration)

`Pending(1)` (row created, channel pending), `Delivered(2)` (InApp persisted / Email would-send logged), `Failed(3)` (channel threw, observable), `SkippedByPreference(4)` (no row persisted, log only — alternative model persists `Skipped` row for audit), `SkippedByPolicy(5)` (policy disabled, no row). MVP persists `Pending|Delivered|Failed` only; `Skipped*` are log-only to avoid bloat (or persisted as `Failed` with reason if audit of skips required).

### DedupeKey (ValueObject)

`record DedupeKey(Guid SourceEventId, Guid RecipientId, int ChannelId)` with value equality. `Equals` is composite. Computed hash `SHA256(SourceEventId|RecipientId|ChannelId)` is not stored — uniqueness is composite unique constraint. Used by `NotificationDeduplicationService` and unit `DedupeKeyEqualityTests`.

### NotificationContent (ValueObject)

`record NotificationContent(string Title, string Body, string Link)` where `Title` 1..200, `Body` 1..2000, `Link` 1..500. Construction validates allowlist per `NotificationType` via `INotificationContentPolicy` (no `DocumentBody`, no `AiPayload`). Value equality.

## Domain Services

### INotificationPolicy

```csharp
bool IsEnabled(NotificationType type, Channel channel, Guid userId,
               IReadOnlyDictionary<int, IReadOnlyDictionary<int,bool>> userPrefs);
IReadOnlySet<(int TypeId, int ChannelId)> MandatedTypes { get; }
IReadOnlyList<Guid> ResolveRecipients(IntegrationEvent @event); // pure map: WorkItemAssigned → assigneeId, DocumentApproved → project watchers etc.
IReadOnlyDictionary<int,IReadOnlyDictionary<int,bool>> DefaultPreferences { get; }
```

Pure, no I/O except config lookup for `MandatedTypes`. Unit-tested via `PolicyMergeTests` matrix (`mandated true → IsEnabled true regardless of pref false`, `pref false → IsEnabled false`, `unset → default true for InApp`).

### IChannelRouter / IChannel

```csharp
interface IChannel { Channel Channel { get; } Task<Result> DeliverAsync(Notification n, CancellationToken ct); }
interface IChannelRouter { Task FanOutAsync(Notification notification, CancellationToken ct); IReadOnlyList<IChannel> Channels { get; } }
```

`FanOutAsync` loops `Channels` try/catch per channel; thrown → log structured + `DeliveryState=Failed` for that channel's row without rolling back other channels.

### INotificationContentPolicy

```csharp
NotificationContent Compose(NotificationType type, IntegrationEvent evt);
```

Allowlist per type; unknown event fields ignored. Validates `INotificationContentPolicyTests` no body leakage.

## Relationships

- `Notification * — 1 NotificationPreference` via `RecipientId==NotificationPreference.UserId` logical (no FK, cross-aggregate read via `INotificationPolicy` at dispatcher time)
- `Notification 1 — 1 IntegrationEvent SourceEventId` logical FK (via dedupe unique, no EF FK cross-schema)
- `NotificationPreference 1 — 0..* Notification` via `UserId == RecipientId` (query-time pre-filter, not EF navigation)
- Logical FK: `Notification.SourceResourceId → projects.work_items.Id` / `documents.documents.Id` / `ai_processing.llm_results.Id` (read-only link builder, no EF FK)
- `NotificationPreference` is per-user, not per-tenant + per-user in PK, but `TenantId` column isolates cross-tenant lookups

## Cross-module contracts consumed

- `Projects.Contracts.Events.WorkItemAssignedIntegrationEvent` etc. (7 work types) — provides `WorkItemId, ProjectId, TenantId, AssigneeId, AssignerId`
- `Documents.Contracts.Events.DocumentUploadedIntegrationEvent` etc. (5 doc types) — provides `DocumentId, DocumentVersionId, TenantId, Classification`
- `AiProcessing.Contracts.Events.LlmResultGeneratedIntegrationEvent` + `RagQueryExecutedIntegrationEvent` — provides `ResultId, OperationId, DocumentId, TenantId, CorrelationId`
- `Metrics.Contracts.Events.RiskIncreasedIntegrationEvent` (when available) — provides `ProjectId, RiskScore, TenantId`
- `Organization.Contracts.IManagementHierarchy` (optional if recipient resolution expands to watchers by hierarchy)
- `TenantContext` (from `Api` / `BuildingBlocks`) — `tenant_id` from JWT first predicate if present on notification rows

## Indexes & DB hardening

- `UNIQUE(SourceEventId, RecipientId, Channel)` — dedupe invariant, caught as `23505`
- `INDEX(RecipientId, CreatedAt DESC)` — `GetMyNotifications` pagination, includes `DeliveryState` filter
- `INDEX(RecipientId) WHERE ReadAt IS NULL` — `GetUnreadCount` count filtered
- `INDEX(CorrelationId)` — tracing
- `RowVersion IsRowVersion()` on `notification_preferences` — optimistic concurrency
- `HasDefaultSchema("notifications")` + `HasNoKey` not used — all entities have PK
- `Npgsql` jsonb for `PreferencesJson`

## Lifecycle

```
IntegrationEvent publish → OutboxProcessor → RabbitMQ → NotificationDispatcher(HandleAsync)
  ├─ ResolveRecipients via INotificationPolicy
  ├─ For each recipient × enabledChannel
  │    ├─ IsEnabled via INotificationPolicy (mandated overrides)
  │    ├─ Compose safe NotificationContent via INotificationContentPolicy (allowlist)
  │    ├─ Attempt INSERT notifications row (unique SourceEventId,RecipientId,Channel)
  │    │    ├─ Success → DeliveryState=Delivered (InApp) / log (Email) → Raise NotificationCreated
  │    │    └─ 23505 duplicate → swallow → log deduped → ack success (idempotent)
  │    └─ Catch channel exception → DeliveryState=Failed row (per-channel) → log structured → ack success (other channels already delivered)
  └─ Ack RabbitMQ manual
User: GetMyNotifications → Specification(RecipientId==callerId) ORDER CreatedAt desc + paginate
User: MarkRead → Load Notification where Id==id && RecipientId==callerId → if not found 404, if callerId != RecipientId 403, if ReadAt==null set ReadAt=UtcNow + Raise NotificationRead
User: UpdatePreferences → Load NotificationPreference where UserId==callerId → validate unknown types → merge PreferencesJson → RowVersion check → UpdatedAt=UtcNow → Raise PreferencesUpdated
```
