# Data Model: Audit, Monitoring and Compliance

**Feature**: 007-audit-monitoring-compliance | **Date**: 2026-09-01 | **Schema**: `audit` (`AuditDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("audit")` + `ApplyConfiguration(new OutboxEntityTypeConfiguration())` for `audit_consumed_events` dedup)

## Entities

### 1. AuditEntry (AggregateRoot/Entity, BC-08, `audit.audit_entries`) — append-only, immutable by design

Root identity, immutable (no setters, only constructor). Corrections are new entries with `Action=AuditCorrected`. If hash chaining adopted per ADR-007-01, `PreviousHash`/`Hash` are populated; otherwise `NULL`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AuditId` | `AuditEntryId : StronglyTypedId<Guid>` | PK, `Guid.NewGuid()` on consumer `AddAsync` | Immutable |
| `Timestamp` | `DateTime` | UTC, required, indexed `DESC` + composite `(TenantId, Timestamp DESC)` | Query ordering `Timestamp` desc for search, asc for trail/timeline |
| `ActorId` | `Guid` | required, from `ActorReference.ActorId` (JWT `sub` or `System` `000…001`) | Actor for audit search filter |
| `ActorType` | `AuditActorType : Enumeration` | required, `User(1)|System(2)|Anonymous(3)` | `ActorReference.ActorType` |
| `ActorDisplayName` | `string` | 1–200, masked if needed | Snapshot |
| `Action` | `AuditAction : Enumeration` | required, 31 values R2 catalog, indexed | Filter dimension |
| `ResourceType` | `string` | 1–100, required, indexed `(ResourceType, ResourceId)` | e.g., `Document`, `Project`, `LlmOperation` |
| `ResourceId` | `string` | 1–200, required | Guid string or composite `projectId/workItemId` |
| `OrganizationId` | `Guid?` | nullable, indexed `OrganizationId` where not null | Snapshot from business event `OrganizationId`; null for tenant-global actions (e.g., `AuthenticationFailed`) |
| `TenantId` | `Guid` | required, indexed `(TenantId, Timestamp DESC)` via composite | Isolation — every query/spec includes it; cross-tenant → 404 |
| `Result` | `AuditResultType : Enumeration` | required, `Success(1)|Denied(2)|Failed(3)` | `AuditResult.Result` |
| `ErrorCode` | `string?` | 1..100, nullable | e.g., `Document.Forbidden` |
| `CorrelationId` | `Guid` | required, indexed | From `Activity.Baggage` + `TenantContext.CorrelationId` (`X-Correlation-Id` header or generated `Guid.NewGuid()`) |
| `ProjectId` | `Guid?` | nullable, indexed where not null | Snapshot from business event `ProjectId` for Golden Rule A query auth `BuildAuthorizedFilter` composition |
| `IpAddress` | `string?` | 1..45, nullable | `ClientMetadata.IpAddress` (IPv4/IPv6, masked `192.168.1.xxx` if EU policy) — null for background jobs (`System` actor) |
| `UserAgent` | `string?` | max 500, nullable | `ClientMetadata.UserAgent` — null for background |
| `BeforeJson` | `string`/`jsonb` | 1..50k, `jsonb` masked | `BeforeAfterSnapshot.BeforeJson` via `IAuditMaskingPolicy.Mask` (`ApiKey`→`***`) |
| `AfterJson` | `string`/`jsonb` | 1..50k, `jsonb` masked | `BeforeAfterSnapshot.AfterJson` |
| `PreviousHash` | `string?` | 64 hex `^[0-9a-f]{64}$` if chaining, else `NULL` | `SHA256(PreviousHash\|\|AuditId\|\|Timestamp\|\|Action\|\|ActorId)` tail lock |
| `Hash` | `string?` | 64 hex if chaining, else `NULL` | Computed `SHA256(PreviousHash...)` |
| `OrganizationName` | `string?` | 1..200 | Snapshot `OrganizationName` for display without join |
| `RowVersion` | `byte[]` | `IsRowVersion()` but conceptually immutable — no `Update` path uses it; `audit_consumed_events` uses `EventId` for idempotency instead | Optimistic concurrency not applicable to immutable entry, but `AuditEntry` still has `RowVersion` for EF |

**Immutability invariants**: Zero public setters (`GetProperties().Where(p=>p.SetMethod?.IsPublic).Count==0`), `IRepository<AuditEntry>` exposes only `AddAsync(AuditEntry)` + `SaveChangesAsync` (no `Update`/`Remove`), `AuditDbContext` `OnModelCreating` does not map `Update` behavior (no `Entry(entity).State=Modified` path compiles for `AuditEntry`), DB app role `REVOKE UPDATE, DELETE ON audit.audit_entries` if no hash chain (else hash chain recomputation would detect `DELETE` + recompute). Verified by `AuditEntryIsImmutableTests` (reflection).

**Events**: none — `AuditEntry` is terminal record; it is produced by `AuditEventConsumer` from upstream `DomainEvent→IntegrationEvent` (e.g., `DocumentApprovedIntegrationEvent` → `AuditEntry Action=DocumentApproved`), not by its own `DomainEvent`.

**Indexes**: `PRIMARY KEY (AuditId)`, `INDEX (TenantId, Timestamp DESC)` for `SearchAuditEntries` <300ms 1k paginated, `INDEX (ResourceType, ResourceId, Timestamp ASC)` for `GetAuditTrail`, `INDEX (CorrelationId, Timestamp ASC)` for `GetOperationTimeline`, `INDEX (TenantId, OrganizationId)` where `OrganizationId IS NOT NULL`, `INDEX (TenantId, ProjectId)` where `ProjectId IS NOT NULL`, `INDEX (TenantId, Action)`, `INDEX (TenantId, ActorId)`.

### 2. AuditConsumedEvent (Entity, BC-08, `audit.audit_consumed_events`) — idempotency dedup for at-least-once `IEventBus`

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `EventId` | `Guid` | PK, `UNIQUE(EventId)` `PRIMARY KEY` | `IntegrationEvent.Id` (stable per `IOutboxWriter.StageAsync` serialization) |
| `ProcessedAt` | `DateTime` | UTC, required, default `UtcNow` | Deduplication timestamp |
| `Action` | `string` | 1..100, e.g., `DocumentApproved` | For debug, not for query |
| `CorrelationId` | `Guid` | required | For trace, not for dedup key |

**Invariants**: `EventId` is globally unique per `IntegrationEvent` instance; duplicate delivery (same `EventId`) attempts `INSERT INTO audit_consumed_events (EventId)` with `UniqueConstraintViolation` → consumer catches and returns success without second `AuditEntry` (idempotent). Concurrent duplicate race on same `EventId` with `INSERT` + `SELECT FOR UPDATE` on dedup table serializes: first `INSERT` succeeds, second gets `UNIQUE` violation and is treated as success. `ProcessedAt` is for GC (if ever, out of scope) but not for query.

**Indexes**: `PRIMARY KEY (EventId)` only.

### 3. Supporting Value Objects (stored as jsonb in `AuditEntry` or as columns)

- **`ActorReference` VO**: `ActorId` Guid, `ActorType` `User|System|Anonymous` (`Enumeration`), `DisplayName` 1..200 masked if needed — stored as `ActorId`/`ActorType`/`ActorDisplayName` columns (not jsonb) for index `ActorId`.
- **`ResourceReference` VO**: `ResourceType` 1..100 + `ResourceId` string 1..200 — stored as `ResourceType`/`ResourceId` columns.
- **`BeforeAfterSnapshot` VO**: `BeforeJson`/`AfterJson` `jsonb` 1..50k masked via `IAuditMaskingPolicy` (`ApiKey`→`***`) — `GetEqualityComponents` includes both JSON normalized via `JsonDocument` canonical serialization (no whitespace).
- **`AuditResult` VO**: `Result` `Success|Denied|Failed` (`Enumeration`), `ErrorCode` 1..100 optional — stored as `Result` int + `ErrorCode` string.

## Enumerations

### AuditAction (Enumeration, 31 values, `audit.audit_action` lookup table or `Enumeration` per `BuildingBlocks.Kernel.Domain.Enumerations.Enumeration<AuditAction>`)

| Id | Name | Maps to Domain/Integration Event (example) | R2 Category |
|----|------|---------------------------------------------|-------------|
| 1 | `AuthenticationSucceeded` | `UserLoggedInIntegrationEvent` | authentication outcomes |
| 2 | `AuthenticationFailed` | `UserLoginFailedIntegrationEvent` | authentication outcomes |
| 3 | `AuthorizationDenied` | `DocumentAccessDeniedIntegrationEvent`, `AuthorizationFailed` | authorization denials |
| 4 | `ProjectCreated` | `ProjectCreatedIntegrationEvent` | project creation |
| 5 | `ProjectUpdated` | `ProjectUpdatedIntegrationEvent` | project modification |
| 6 | `WorkItemCreated` | `WorkItemCreatedIntegrationEvent` | work-item creation |
| 7 | `WorkItemUpdated` | `WorkItemUpdatedIntegrationEvent` | work-item modification |
| 8 | `WorkItemAssigned` | `WorkItemAssignedIntegrationEvent` | assignment |
| 9 | `WorkItemStatusChanged` | `WorkItemStatusChangedIntegrationEvent` | status |
| 10 | `ProjectMetricChanged` | `MetricCalculatedIntegrationEvent` | metric changes |
| 11 | `DocumentUploaded` | `DocumentUploadedIntegrationEvent` | document lifecycle |
| 12 | `DocumentClassified` | `DocumentClassifiedIntegrationEvent` | document lifecycle |
| 13 | `DocumentVersionPublished` | `DocumentVersionPublishedIntegrationEvent` | document lifecycle |
| 14 | `DocumentAccessed` | `DocumentAccessedIntegrationEvent` | document lifecycle (grant) |
| 15 | `DocumentAccessDenied` | `DocumentAccessDeniedIntegrationEvent` | document lifecycle (denial) |
| 16 | `DocumentDeleted` | `DocumentDeletedIntegrationEvent` | document lifecycle |
| 17 | `DocumentApproved` | `DocumentApprovedIntegrationEvent` | document lifecycle |
| 18 | `PermissionChanged` | `PermissionUpdatedIntegrationEvent` | permission and grant changes |
| 19 | `GrantAdded` | `ExplicitGrantAddedIntegrationEvent` | permission and grant changes |
| 20 | `GrantRevoked` | `ExplicitGrantRevokedIntegrationEvent` | permission and grant changes |
| 21 | `HierarchyChanged` | `HierarchyChangedIntegrationEvent` | hierarchy changes |
| 22 | `LlmOperationQueued` | `LlmOperationQueuedIntegrationEvent` | AI operations |
| 23 | `LlmOperationCompleted` | `LlmOperationCompletedIntegrationEvent` | AI operations |
| 24 | `LlmOperationFailed` | `LlmOperationFailedIntegrationEvent` | AI operations |
| 25 | `LlmResultGenerated` | `LlmResultGeneratedIntegrationEvent` | AI results |
| 26 | `LlmResultApproved` | `LlmResultApprovedIntegrationEvent` | AI review decisions |
| 27 | `LlmResultRejected` | `LlmResultRejectedIntegrationEvent` | AI review decisions |
| 28 | `LlmReviewCreated` | `LlmReviewCreatedIntegrationEvent` | AI review decisions |
| 29 | `RagQueryExecuted` | `RagQueryExecutedIntegrationEvent` | AI operations |
| 30 | `ConfigurationChanged` | `PromptVersionPublishedIntegrationEvent`, `ReviewPolicyChanged`, `ConfigurationUpdated` | configuration changes |
| 31 | `AuditCorrected` | (correction entry itself, not a business event) | corrections new entries |

**Indexes**: `AuditAction` is FK to `AuditEntry.Action`; `Enumeration` per `BuildingBlocks.Kernel.Domain.Enumerations.Enumeration<AuditAction>` with `FromId/FromName`.

### AuditResultType (Enumeration)

`Success(1)`, `Denied(2)`, `Failed(3)` — stored as `int` `Result` in `audit_entries`.

### AuditActorType (Enumeration)

`User(1)`, `System(2)`, `Anonymous(3)` — stored as `int` `ActorType`.

## Relationships

- `AuditEntry` has **no FK** to business tables (`Project`, `Document`, `LlmOperation`) — it stores `ResourceType`/`ResourceId` string snapshot (loose coupling, not `HasOne` FK). This keeps `Audit` BC supporting + decoupled (Principle V: no cross-module DbContext FK). Snapshot includes `OrganizationId`/`TenantId` at emission time for `IAuditQueryAuthorization` `BuildAuthorizedFilter` composition without live join.
- `AuditConsumedEvent` has **no FK** to `AuditEntry` — it dedups `IntegrationEvent.Id` independently of `AuditEntry.AuditId` (different `Guid` namespaces). One `IntegrationEvent` → one `AuditConsumedEvent` + one `AuditEntry` (atomic in same `AuditDbContext` transaction).
- `AuditEntry` **self-reference** for hash chaining (if adopted): `AuditEntry.PreviousHash = SELECT Hash FROM audit_entries ORDER BY Timestamp DESC LIMIT 1 FOR UPDATE` (tail lock) — not an FK, but a value reference to prior entry's `Hash`.

## Cross-module contracts consumed (read-only)

- `IManagementHierarchy` (from `Organization.Contracts`): `GetSubtreeIds(actorId)→IReadOnlySet<Guid>` + `IsInSubtree(ancestor, descendant)` — used by `IAuditQueryAuthorization.BuildAuthorizedFilter` to compute `authorizedOrgIds = subtree(actorId)` for audit `OrganizationId` filter. Tenant-aware stub in tests.
- `IProjectMembership` (from `Projects.Contracts` via `Organization.Domain.Services`): `GetProjectIds(actorId)` + `IsMember(projectId,actorId)` — used for `authorizedProjectIds` set for `ProjectId` filter. Thin adapter reading `projects.project_members` stub in tests.
- `IAuthorizationEvaluator` (from `Organization.Infrastructure.Services`): `CanActorPerform(actor, "audit.search")` for `auditor|manager` role granting tenant-wide where `authorizedOrgIds = allOrgsInTenant` (branch-scoped otherwise).
- `TenantContext` (from `Api.Tenant`): `TenantId` + `CorrelationId` (`Guid`) — first predicate in every `Specification<AuditEntry>`; `CorrelationId` from `Activity.Baggage` (`X-Correlation-Id` header) or generated at middleware entry.
- `IEventBus` (`BuildingBlocks.EventBus`) + `IntegrationEvent` (`BuildingBlocks.EventBus.Abstractions`): `EventId`, `OccurredOnUtc`, `CorrelationId` — for `AuditEventConsumer` dedup.

## Indexes & Performance Mapping

- `SearchAuditEntries` uses `WHERE TenantId=@t AND (OrganizationId IN @authorizedOrgIds) AND (ProjectId IN @authorizedProjectIds) AND ActorId=@actorId? AND Action=@action? AND ResourceType=@rt? AND ResourceId=@rid? AND Result=@result? AND CorrelationId=@cid? AND Timestamp BETWEEN @from AND @to ORDER BY Timestamp DESC OFFSET @skip LIMIT @take` — composite `INDEX (TenantId, Timestamp DESC)` satisfies `TenantId` equality + `Timestamp` ordering + pagination `<300ms p95` for 1k (verified via `EXPLAIN ANALYZE`).
- `GetAuditTrail` uses `WHERE TenantId=@t AND ResourceType=@rt AND ResourceId=@rid AND TenantId IN authorized filter ORDER BY Timestamp ASC` — `INDEX (ResourceType, ResourceId, Timestamp ASC)` + `TenantId` predicate pushes down to index scan.
- `GetOperationTimeline` uses `WHERE CorrelationId=@cid AND TenantId=@t AND (OrganizationId IN authorizedOrgIds) ORDER BY Timestamp ASC` — `INDEX (CorrelationId, Timestamp ASC)` + `TenantId` filter.
- All three share `AuditByTenantSpec` base (`Where(a=>a.TenantId==tenantId)`) composed via `Specification<AuditEntry>.And`.

## Tamper-evidence variant (per ADR-007-01, not default)

- If hash chaining adopted: `AuditEntry` gets `PreviousHash` (64 hex `PreviousEntry.Hash` or `0…0` for first) + `Hash = SHA256(PreviousHash + "|" + AuditId + "|" + Timestamp.ToString("O") + "|" + Action + "|" + ActorId)` (UTF8, lower hex). Insert transaction does `SELECT Hash FROM audit_entries WHERE TenantId=@t ORDER BY Timestamp DESC LIMIT 1 FOR UPDATE` to serialize tail, computes `PreviousHash`, inserts new row with `Hash`. `VerifyChain()` does `SELECT AuditId, PreviousHash, Hash, Timestamp, Action, ActorId FROM audit_entries WHERE TenantId=@t ORDER BY Timestamp ASC` then recomputes chain and returns first mismatch (`AuditId`, `ExpectedHash`, `ActualHash`) or `Success`. This variant adds per-insert tail lock contention (100 events/sec still okay with `FOR UPDATE` short critical section, but high write concurrency across tenants serializes per-tenant — acceptable for audit).
- If `REVOKE` adopted (default): `PreviousHash`/`Hash` stay `NULL`, `VerifyChain()` returns `NotApplicable`, and `REVOKE UPDATE, DELETE ON audit.audit_entries FOR app_orokanban` is applied in migration (`GRANT SELECT, INSERT ON audit.* TO app_orokanban`).
