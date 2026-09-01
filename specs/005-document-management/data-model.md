# Data Model: Document Management

**Feature**: 005-document-management | **Date**: 2026-09-01 | **Schema**: `documents` (`DocumentsDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("documents")` + `ApplyConfiguration(new OutboxEntityTypeConfiguration())`)

## Entities

### 1. Document (AggregateRoot, BC-05, `documents.documents`)

Root identity, points to current version, owns lifecycle and provenance. Concurrency via `RowVersion`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `DocumentId : StronglyTypedId<Guid>` | PK, `Guid.NewGuid()` on `UploadDocument` | Root identifier |
| `TenantId` | `Guid` | required, from `TenantContext` (JWT `tenant_id`) | Tenant isolation — every query/spec includes it; cross-tenant → 404 |
| `OrganizationId` | `Guid` | required, from tenant's org | Organization scope for classification extensions |
| `OwnerId` | `Guid` | required, FK to OroIdentityServer user (`sub`) | Owner for access OR grant; feeds `IDocumentAccessPolicy` |
| `Name` | `string` | required, 1–300 chars, trimmed | Display name (original filename sanitized) |
| `ClassificationLevel` | `int` | FK `ClassificationLevel` Enumeration, required | Denormalized from policy result; `DocumentClassification` naming alias |
| `ClassificationValue` | `string` | 1–100, required | Level name e.g. `Confidential` or `TopSecretFinance` |
| `RuleVersion` | `string` | required, 1–20, e.g. `v3` | `IClassificationPolicy` version used |
| `CurrentVersionId` | `DocumentVersionId` | FK `documents.document_versions`, required, indexed | Pointer to `DocumentVersion` with max `VersionNumber` |
| `Status` | `DocumentStatus : Enumeration` | required, default `Uploaded` | Lifecycle — see transition map below |
| `MimeType` | `string` | 1–255, `^[-+.\w]+/[-+.\w]+$`, required | `MimeType` VO value |
| `Size` | `long` | `>=0`, required | Bytes — from content-length/hash stage |
| `ContentHash` | `string` | 64 chars `^[0-9a-f]{64}$`, required, indexed | `ContentHash` VO — deduplication key for object storage |
| `ProjectId` | `Guid?` | nullable, indexed, FK logical to `projects.projects` | Optional linkage — validated via `IProjectMembership` existence + tenant match |
| `WorkItemId` | `Guid?` | nullable, indexed, FK logical to `projects.work_items` | Optional — same validation as ProjectId |
| `ProvenanceSource` | `string` | 1–200, required | `Provenance.Source` (e.g. `upload`, `api`) |
| `OriginalFilename` | `string` | 1–300, required | `Provenance.OriginalFilename` |
| `CreatedBy` | `Guid` | required | Actor (`sub`) who uploaded |
| `CreatedAt` | `DateTime` | UTC, required | `AppDbContextBase` audit |
| `UpdatedAt` | `DateTime` | UTC | Updated by context |
| `DeletedAt` | `DateTime?` | nullable | Set on soft delete |
| `DeletedBy` | `Guid?` | nullable | Actor who deleted |
| `RetentionRetainUntil` | `DateTime?` | nullable | `RetentionPolicy.RetainUntil` |
| `RetentionDays` | `int?` | nullable, `>0` | `RetentionPolicy.RetentionDays` |
| `RetentionLegalHold` | `bool` | default false | `RetentionPolicy.LegalHold` |
| `IsSafe` | `bool` | derived, default false | `Document.IsSafe` = `CurrentVersion.IsSafe` — false hasta `VirusScan` clean → `Safe`; lectura bloqueada si false (clarificación 2026-09-01) |
| `ScanStatus` | `int` | FK `ScanStatus : Enumeration` (`Pending|Safe|Infected|Unavailable`), default `Pending` | Estado escaneo antivirus por versión actual |
| `ScannedAt` | `DateTime?` | nullable UTC | Cuando se marcó seguro/infectado |
| `ScannedBy` | `Guid?` | nullable | Actor/sistema que escaneó |
| `RowVersion` | `byte[]` | `IsRowVersion()` concurrency token | Optimistic concurrency — `PublishDocumentVersion`/`ClassifyDocument` race → 409 |

**Status lifecycle** (`DocumentStatus : Enumeration`, enforced by `DocumentStatusTransitionRule : IBusinessRule` via `Document.ChangeStatus` + `CheckRule`):

```
Draft(1) → Uploaded(2) → Validated(3) → Classified(4) → Indexed(5) → Available(6) → PendingApproval(7) → Approved(8)
Uploaded|Validated|Classified|Indexed|Available|PendingApproval → ProcessingFailed(9)  (on any stage failure)
ProcessingFailed → Validated  (on retry success path — re-validate then re-walk)
Available|Classified|PendingApproval|ProcessingFailed → Deleted(11)   (soft delete — never erases rows)
Available → Archived(10)
* → RetentionExpired(12)  (flag when RetentionPolicy.IsExpired(now) && !LegalHold — query-time flag; explicit Archive/Delete still required)
Any edge not listed → Error.BusinessRule("Transition not allowed: {from}→{to}")
```

**Events (domain → outbox → integration)**: `DocumentUploaded {DocumentId, TenantId, OwnerId, ProjectId, Hash}`, `DocumentValidated`, `DocumentMarkedSafe {DocumentId, VersionId, ScannedAt}`, `DocumentScanFailed {DocumentId, VersionId, Reason, ScanStatus}`, `DocumentClassified {DocumentId, Classification, RuleVersion}`, `DocumentAccessed {DocumentId, ActorId, Classification}`, `DocumentAccessDenied {DocumentId, ActorId, Reason, Classification}` (incluye `NotSafe`), `DocumentDeleted {DocumentId, ActorId, DeletedAt}`, `DocumentApproved {DocumentId, ApproverId, ApprovedAt}`. Also `DocumentStatusChanged` generic if not sub-typed. Lectura denegada por `IsSafe=false` emite `DocumentAccessDenied(reason=NotSafe)` aunque Golden Rule A pase.

**Indexes**: `UNIQUE (TenantId, Id)` (PK already), `INDEX (TenantId, ProjectId)`, `INDEX (TenantId, OwnerId)`, `INDEX (ContentHash)`, `INDEX (Status)`.

### 2. DocumentVersion (AggregateRoot, BC-05, `documents.document_versions`)

Immutable once `IsPublished=true`. Append-only; each correction is a new row with `VersionNumber` monotonic per `DocumentId`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `DocumentVersionId : StronglyTypedId<Guid>` | PK | |
| `DocumentId` | `DocumentId` | FK `documents.documents`, required, indexed | Parent |
| `VersionNumber` | `int` | required, `>=1`, `UNIQUE (DocumentId, VersionNumber)` | Monotonic v1..n |
| `ContentHash` | `string` | 64 hex, required | May equal prior version hash if bytes deduped but metadata differs |
| `MimeType` | `string` | 1–255, required | Snapshot from upload/publish |
| `Size` | `long` | `>=0` | Snapshot |
| `IsPublished` | `bool` | required, default true after Validation | Guard for `VersionIsImmutableOncePublishedRule` |
| `PublishedAt` | `DateTime` | UTC, required | Snapshot |
| `PublishedBy` | `Guid` | required | Actor who published |
| `RuleVersion` | `string` | 1–20, required | Copied from Document classification at publish time |
| `MetadataSnapshotJson` | `jsonb` | required, Npgsql jsonb, owned VO | `MetadataSnapshot` serialized (see VO below) — includes Author/Dept/Tags/Type/Dates/Source/Confidentiality/Retention/CustomMetadata |
| `MetadataEffectiveDate` | `DateTime?` | nullable, indexed | Extracted from snapshot for retention queries |
| `MetadataExpirationDate` | `DateTime?` | nullable | `EffectiveDate <= ExpirationDate` enforced in VO |
| `IsSafe` | `bool` | default false, indexed | **Bloqueo de lectura** — false hasta `VirusScan` clean → `Safe` (clarificación 2026-09-01); `GetDocument`/`Download`/`IStorageGateway` deniegan si false con `NotSafe` |
| `ScanStatus` | `int` | FK `ScanStatus : Enumeration` (`Pending(0)|Safe(1)|Infected(2)|Unavailable(3)`), default `Pending` | Por-versión; inmutable excepto transición `Pending→Safe/Infected` vía `MarkSafe` |
| `ScannedAt` | `DateTime?` | nullable UTC | Timestamp del escaneo |
| `ScannedBy` | `Guid?` | nullable | Sistema/actor que escaneó |
| `CreatedAt` | `DateTime` | UTC | DB audit |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency on creation path only — versiones nunca mutadas después de publish (IsSafe se actualiza vía método de dominio audited, no UPDATE directo) |

**Invariants**: `IsPublished==true` → all setters throw `VersionIsImmutableOncePublishedRule` (except `IsSafe`/`ScanStatus` transition `Pending→Safe/Infected` vía `MarkSafe` con `CheckRule`); `VersionNumber` is max+1 per Document; `MetadataSnapshot` validated (`IBusinessRule`) at publish; `PublishedAt/By` never null cuando `IsPublished`; `IsSafe=false` por defecto y solo `VirusScan` clean puede poner `Safe`.

**Events**: `DocumentVersionPublished {DocumentVersionId, DocumentId, VersionNumber, Hash, PublishedBy}`, `DocumentVersionSuperseded {DocumentVersionId, SupersededByVersionId}`, `DocumentVersionMarkedSafe {DocumentVersionId, ScannedAt, ScannedBy}`, `DocumentVersionScanFailed {DocumentVersionId, Reason, ScanStatus}`.

**Guard test**: loading a published version and mutating `ContentHash`/`MetadataSnapshotJson` via repository `Update` must be rejected at domain setter (throws) and EF change tracker never marks dirty — verified by reload equality (SC-002).

### 3. DocumentProcessingJob (AggregateRoot, BC-05, `documents.document_processing_jobs`)

One job per `DocumentVersion` (initially per Document — v1). Tracks per-stage resumable status.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `DocumentProcessingJobId : StronglyTypedId<Guid>` | PK | |
| `DocumentId` | `DocumentId` | FK documents, required, indexed | |
| `DocumentVersionId` | `DocumentVersionId` | FK versions, required | The version being processed |
| `TenantId` | `Guid` | required | Tenant isolation |
| `CurrentStage` | `ProcessingStage : Enumeration` | required, default `Validation(2)` | Next/running stage |
| `StageStatusesJson` | `jsonb` | required | Map `ProcessingStage → StageStatus` (`Pending(0)|InProgress(1)|Succeeded(2)|FailedRetryable(3)|FailedPermanent(4)`) per stage + `AttemptCount` + `LastError` |
| `OverallStatus` | `int` | `Pending|InProgress|Succeeded|FailedRetryable|FailedPermanent` derived | `Succeeded` only when `Indexing==Succeeded` |
| `AttemptCount` | `int` | `>=0`, default 0 per stage | Incremented on each `FailedRetryable` |
| `LastError` | `string?` | max 1k | Last failure reason (e.g. `Infected`, `ScannerUnavailable`, `HashMismatch`) |
| `LastErrorStage` | `ProcessingStage?` | nullable | Stage that last failed |
| `RuleVersion` | `string` | 1–20 | Snapshot of classification rule version if Classification stage completed |
| `CreatedAt` | `DateTime` | UTC | |
| `UpdatedAt` | `DateTime` | UTC, auto on each stage transition | |
| `CompletedAt` | `DateTime?` | UTC, set when `OverallStatus==Succeeded` | |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency for retry race |

**Stage order & behavior**:

| Stage Enum | Value | Handler | Success next | Failure → `FailedRetryable` → maxAttempts=3 → `FailedPermanent` |
|-----------|-------|---------|--------------|-------------------------------------------------------------------|
| `Upload` | 1 | `UploadDocument` handler (sync accept) | `Validation` | N/A (validation rejects with 400 before job) |
| `Validation` | 2 | `ValidationHandler` (MIME/size/tenant/proj link) | `VirusScan` | `FailedRetryable` with reason `ValidationFailed` |
| `VirusScan` | 3 | `VirusScanHandler` via `ISecurityScanProvider` | `Metadata` | `Infected`/`ScannerUnavailable` — never proceeds to `Storage` |
| `Metadata` | 4 | `MetadataHandler` (extract author/dept/tags/type from snapshot or content) | `Classification` | `FailedRetryable(reason=MetadataExtractionFailed)` |
| `Classification` | 5 | `ClassificationHandler` via `IClassificationPolicy` | `Storage` | `FailedRetryable(reason=ClassificationFailed)` |
| `Storage` | 6 | `StorageHandler` via `IStorageGateway.PutAsync` + hash verify | `Indexing` | `FailedRetryable(reason=HashMismatch|StorageUnavailable)` idempotent by `ContentHash` |
| `Indexing` | 7 | `IndexingHandler` (publish `DocumentIndexedIntegrationEvent` for BC-07) | terminal `Succeeded` | `FailedRetryable(reason=IndexingFailed)` |

**Events**: `DocumentProcessingStageCompleted {JobId, Stage}`, `DocumentProcessingFailed {JobId, Stage, Reason, Retryable, AttemptCount}`, `DocumentProcessingSucceeded {JobId}` (when Indexing Succeeded).

### 4. DocumentAccessEntry (Entity, `documents.document_access_entries`) — append-only access history

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK `Guid.NewGuid()` | Surrogate |
| `DocumentId` | `DocumentId` | FK documents, required, indexed | |
| `TenantId` | `Guid` | required, indexed | Isolation |
| `ActorId` | `Guid` | required | `sub` who attempted access |
| `Action` | `int` | `Read(1)|Download(2)|Denied(3)` | `Denied` for both read/download denials (reason distinguishes) |
| `Granted` | `bool` | required | true for Read/Download, false for Denied |
| `ClassificationValue` | `string` | 1–100, required | Classification at time of access |
| `RuleVersion` | `string` | 1–20 | Rule version at time |
| `Reason` | `string?` | max 200 | `NotInSubtreeOrMembership|InsufficientClassification|Deleted|NotFound|TenantMismatch` etc. Generic reason, no internal detail leak |
| `Timestamp` | `DateTime` | UTC, required, indexed descending | |
| `IpAddress` | `string?` | 1–45 | Optional — from HttpContext |
| `UserAgent` | `string?` | max 500 | Optional |

**Backing `GetAccessHistory`**: `WHERE DocumentId==id AND TenantId==ctx.TenantId ORDER BY Timestamp ASC` with pagination (`page/pageSize`). Scoped to auditor (`document.audit.read`) or owner.

**Events backing**: every `GetDocument`/`DownloadDocument` read creates one entry via `DocumentAccessed`/`DocumentAccessDenied` handling — same-tx with outbox so history is never lost.

### 5. DocumentExplicitGrant (Entity, `documents.document_explicit_grants`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `DocumentId` | `DocumentId` | FK documents, required, indexed | |
| `TenantId` | `Guid` | required | |
| `GranteeUserId` | `Guid` | required | Actor who gains access regardless of subtree/membership |
| `GrantedBy` | `Guid` | required | Actor who issued grant |
| `GrantedAt` | `DateTime` | UTC | |
| `ExpiresAt` | `DateTime?` | nullable | If set, grant expires; `IsExpired(now)` check in policy |
| `RevokedAt` | `DateTime?` | nullable | Soft revoke — not deleted |

**Unique**: `UNIQUE (DocumentId, GranteeUserId) WHERE RevokedAt IS NULL`.

### 6. ClassificationRule (Entity, `documents.classification_rules`) — versioned policy

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | |
| `OrganizationId` | `Guid?` | nullable — null = system default | Null means global default set |
| `Version` | `string` | 1–20, e.g. `v3`, unique per `OrganizationId` | Opaque version |
| `EffectiveFrom` | `DateTime` | UTC, required | When version became current |
| `IsCurrent` | `bool` | required | Exactly one `true` per `OrganizationId` |
| `RuleSetJson` | `jsonb` | required | `{ defaultLevels: [...], orgExtensions: [{ level:"TopSecretFinance", value:101, label:"Top Secret Finance"}], rules: [...] }` |
| `CreatedAt` | `DateTime` | UTC | |
| `CreatedBy` | `Guid` | | Who changed rules |

**Seed**: `Public|Internal|Confidential|Restricted|HighlyRestricted` as `ClassificationLevel : Enumeration` with `IsCurrent` default `v1` per tenant's org.

## Value Objects & Enumerations (Domain invariants — validate at construction)

### Classification (VO) — composite

- `Level: ClassificationLevel : Enumeration` (Public=1..HighlyRestricted=5 + org extensions 101+)
- `Value: string` (level name)
- `RuleVersion: string` (e.g. `v3`) — stamped on Document/Version
- Invariant: `Level` must be in `IClassificationPolicy.AllowedLevels(organizationId)`; unknown → `Error.Validation`; ordering `IsMoreSensitiveThan` compares `Level` numeric value for access clearance.

### ContentHash (VO)

- `Value: string` 64 chars `^[0-9a-f]{64}$` (SHA-256 hex, lowercased), value-equality.
- `ContentHash.FromBytes(byte[])` computes `SHA256.HashData`.
- Invalid → `Error.Validation`.

### MimeType (VO)

- `Value: string` 1–255, pattern `^[-+.\w]+/[-+.\w]+$`.
- `Extension: string` derived (`pdf` from `application/pdf`).
- Allow-list configurable (see research); `application/octet-stream` is fallback with `warn` but not blocked unless policy says.

### RetentionPolicy (VO)

- `RetainUntil: DateTime?` (UTC), `RetentionDays: int? (>0)`, `LegalHold: bool`.
- `ComputeRetainUntil(EffectiveDate?) => RetainUntil ?? (EffectiveDate + RetentionDays)`.
- `IsExpired(now) => !LegalHold && RetainUntil != null && now >= RetainUntil`.
- `IsExpired` is query-time derived; no auto-delete.

### MetadataSnapshot (VO) — stored as jsonb on DocumentVersion

- `Author: string?` 1–200, `Department: string?` 1–200, `ProjectText: string?` 1–200 (text mirror of `Document.ProjectId`), `Tags: IReadOnlySet<string>` (trimmed/lowercased 1–50/`^[a-z0-9_-]+$`/≤50/unique), `DocumentType: string?` 1–100, `EffectiveDate: DateTime?`, `ExpirationDate: DateTime?` (`Effective <= Expiration`), `Source: string?` 1–200, `Confidentiality: string?` 1–100, `RetentionPolicy: RetentionPolicy`, `CustomMetadata: IReadOnlyDictionary<string,string>` (key ≤64/value ≤2KB/≤50 entries).
- Invariants in constructor; `GetEqualityComponents()` covers all fields (Tags ordered, CustomMetadata ordered).

### Provenance (VO) — embedded in Document

- `Source: string` 1–200 (e.g. `upload`), `OriginalFilename: string` 1–300, `UploadedBy: Guid`, `UploadedAt: DateTime`.
- Required on create.

### DocumentStatus (Enumeration) — see lifecycle map in Document section (12 values, seeded `Enumeration` table).

### ProcessingStage (Enumeration) — 7 values (Upload..Indexing), seeded `Enumeration` table, value `int 1..7`.

### DocumentType (Enumeration/string hybrid) — seed set (`Contract|Invoice|Report|Design|Other`) + `Other` fallback for free-form `DocumentType` in snapshot.

## Relationships

- `Document 1 — * DocumentVersion` via `DocumentId`; `Document.CurrentVersionId` FK to versions (circular, enforced deferrable or application-level; EF config `HasOne<Document>().WithOne().HasForeignKey<Document>(d=>d.CurrentVersionId)` with `DeleteBehavior.Restrict`).
- `Document 1 — * DocumentProcessingJob` via `DocumentId` (and `DocumentVersionId`).
- `Document 1 — * DocumentAccessEntry` via `DocumentId` (append).
- `Document 1 — * DocumentExplicitGrant` via `DocumentId`.
- Logical FKs: `Document.ProjectId → projects.projects.Id` (read-only check via `IProjectMembership`/Projects read model, no EF FK cross-schema); same for `WorkItemId`.
- `DocumentVersion.DocumentId → documents.documents.Id`.

## Cross-module contracts consumed

- `IManagementHierarchy` (from `Organization.Contracts`): `IsInSubtree(ancestorId, descendantId)` for access OR branch; `GetAncestors`/`GetSubtree` for audit diagnostics. Tenant-aware stub in tests.
- `IProjectMembership` (from `Projects.Contracts`): `IsMember(projectId, userId)` — thin adapter reading `projects.project_members`; consumed by `IDocumentAccessPolicy`.
- `IAuthorizationEvaluator` (permission `document.read|download|upload|approve|audit.read|processing.retry`) — pre-check before domain policy for `RetryProcessingStage`/`ApproveDocument`/`GetAccessHistory`.
- Storage and scan providers are `Documents.Infrastructure`-owned adapters, not cross-module.
