# Tasks: Document Management

**Input**: Design documents from `/specs/005-document-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (4 contracts), quickstart.md
**Branch**: `005-document-management` | **Date**: 2026-09-01

**Organization**: Tasks grouped by user story to enable independent implementation and testing. Each story is independently testable; P1 stories (US1-US3) form MVP core but have ordering dependencies noted.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, Aspire wiring, and module plumbing

- [X] T001 Add MinIO S3-compatible Aspire resource to AppHost in `OroKanban.AppHost/AppHost.cs` (AddMinio or AWS S3 proxy with WithDataVolume, WithReference for api)
- [X] T002 Register DocumentsDbContext in Api DI in `src/Api/Program.cs` (AddDbContext<DocumentsDbContext> with Npgsql connection "orokanban")
- [X] T003 Configure S3/storage options binding in `src/Api/appsettings.json` and `src/Api/appsettings.Development.json` (Documents__BucketName, Documents__PresignedUrlTtlMinutes, AWS__ServiceURL)
- [X] T004 Add Documents module references to Api project in `src/Api/Api.csproj` (ProjectReference Documents.Application, Documents.Infrastructure, Documents.Contracts)
- [X] T005 [P] Create Documents.Tests test project `tests/Documents.Tests/Documents.Tests.csproj` (xUnit, NSubstitute, Testcontainers, NetArchTest, Testcontainers.Minio) with solution reference
- [X] T006 [P] Create Architecture boundary test for Documents module in `tests/Architecture/DocumentsArchitectureTests.cs` (no cross-module Infra refs except Contracts)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain primitives, enumerations, VOs, and persistence scaffolding that MUST be complete before ANY user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T007 Create StronglyTypedIds in `src/Modules/Documents/Documents.Domain/Ids/DocumentIds.cs` (DocumentId, DocumentVersionId, DocumentProcessingJobId : StronglyTypedId<Guid>)
- [X] T008 [P] Create DocumentStatus Enumeration in `src/Modules/Documents/Documents.Domain/Enumerations/DocumentStatus.cs` (Draft=1..Deleted=11, RetentionExpired=12 with lifecycle map)
- [X] T009 [P] Create ProcessingStage Enumeration in `src/Modules/Documents/Documents.Domain/Enumerations/ProcessingStage.cs` (Upload=1..Indexing=7)
- [X] T010 [P] Create ScanStatus Enumeration in `src/Modules/Documents/Documents.Domain/Enumerations/ScanStatus.cs` (Pending=0, Safe=1, Infected=2, Unavailable=3)
- [X] T011 [P] Create ClassificationLevel Enumeration in `src/Modules/Documents/Documents.Domain/Enumerations/ClassificationLevel.cs` (Public=1..HighlyRestricted=5, org extensions 101+)
- [X] T012 [P] Create ContentHash ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/ContentHash.cs` (64 hex SHA-256, Value-equality, FromBytes)
- [X] T013 [P] Create MimeType ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/MimeType.cs` (pattern ^[-+.\w]+/[-+.\w]+$, Extension derived, allow-list)
- [X] T014 [P] Create RetentionPolicy ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/RetentionPolicy.cs` (RetainUntil, RetentionDays, LegalHold, IsExpired, ComputeRetainUntil)
- [X] T015 [P] Create Provenance ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/Provenance.cs` (Source, OriginalFilename, UploadedBy, UploadedAt)
- [X] T016 [P] Create Classification ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/Classification.cs` (Level, Value, RuleVersion, IsMoreSensitiveThan, equality)
- [X] T017 [P] Create MetadataSnapshot ValueObject in `src/Modules/Documents/Documents.Domain/ValueObjects/MetadataSnapshot.cs` (Author, Department, ProjectText, Tags set, DocumentType, Effective/ExpirationDate, Source, Confidentiality, RetentionPolicy, CustomMetadata bag with validation + GetEqualityComponents jsonb)
- [X] T018 Create domain events base and registry in `src/Modules/Documents/Documents.Domain/Events/DocumentDomainEvents.cs` (DocumentUploaded, DocumentValidated, DocumentMarkedSafe, DocumentScanFailed, DocumentClassified, DocumentAccessed, DocumentAccessDenied, DocumentDeleted, DocumentApproved, DocumentVersionPublished, DocumentVersionSuperseded, DocumentProcessingStageCompleted, DocumentProcessingFailed, DocumentVersionMarkedSafe)
- [X] T019 [P] Create business rules in `src/Modules/Documents/Documents.Domain/Rules/DocumentBusinessRules.cs` (VersionIsImmutableOncePublishedRule, DocumentStatusTransitionRule, ClassificationIsValidRule, MetadataSnapshotValidationRule, ProcessingStageTransitionRule, StageIsRetryableRule : IBusinessRule via CheckRule)
- [X] T020 Configure DocumentsDbContext EF mappings skeleton in `src/Modules/Documents/Documents.Infrastructure/Persistence/Configurations/DocumentConfiguration.cs` (HasDefaultSchema documents, RowVersion, owned MetadataSnapshot jsonb, OutboxEntityTypeConfiguration)
- [X] T021 Configure remaining EF entity configurations in `src/Modules/Documents/Documents.Infrastructure/Persistence/Configurations/DocumentVersionConfiguration.cs` (DocumentVersion + IsSafe/ScanStatus, unique DocumentId+VersionNumber)
- [X] T022 Configure DocumentProcessingJob, DocumentAccessEntry, DocumentExplicitGrant, ClassificationRule EF configs in `src/Modules/Documents/Documents.Infrastructure/Persistence/Configurations/JobAndAccessConfigurations.cs` (jsonb StageStatusesJson, indexes TenantId+ProjectId, TenantId+OwnerId, ContentHash)
- [X] T023 Create core Specifications in `src/Modules/Documents/Documents.Infrastructure/Specifications/DocumentSpecifications.cs` (DocumentByTenantSpec, DocumentByTenantAndIdSpec, AuthorizedDocumentSpec, AccessHistorySpec with tenant filter)
- [X] T024 Create Integration event contracts in `src/Modules/Documents/Documents.Contracts/Events/DocumentIntegrationEvents.cs` (DocumentUploadedIntegrationEvent, DocumentVersionPublishedIntegrationEvent, DocumentClassifiedIntegrationEvent, DocumentAccessedIntegrationEvent, DocumentAccessDeniedIntegrationEvent, DocumentDeletedIntegrationEvent, DocumentApprovedIntegrationEvent, DocumentIndexedIntegrationEvent, DocumentProcessingStageCompleted/Failed/StageRequestedIntegrationEvents : IntegrationEvent)

**Checkpoint**: Foundation ready - user story implementation can now begin (US1 first due to dependency chain)

---

## Phase 3: User Story 1 - Upload document with async outbox pipeline (Priority: P1) 🎯 MVP

**Goal**: Owner uploads a file → Document + DocumentVersion v1 persisted + binary staged by ContentHash in S3 + DocumentProcessingJob queued via transactional outbox without blocking HTTP (<500ms, SC-001)

**Independent Test**: POST `UploadDocument` (name=contract.pdf, mime=application/pdf, projectId, classificationHint) → 202 with documentId+versionId in <500ms, status=Uploaded, CurrentStage=Validation; GetDocument shows Document + v1 with ContentHash/MimeType/size/currentVersionPointer=v1; MinIO holds blob keyed by sha256/{hash}.{ext}; DocumentProcessingJob exists with Upload=Succeeded, Validation=Pending; zero VirusScan/Classification invocations in-request (verified by timing + fake invocation counts)

### Tests for User Story 1

- [X] T025 [P] [US1] Unit test for ContentHash/MimeType/RetentionPolicy VOs in `tests/Documents.Tests/Unit/ValueObjectsTests.cs`
- [X] T026 [P] [US1] Integration test for UploadDocument HTTP acceptance <500ms with outbox in `tests/Documents.Tests/Integration/UploadPipelineTests.cs`
- [X] T027 [P] [US1] Integration test for deduplication (identical bytes share ContentHash, one blob) in `tests/Documents.Tests/Integration/DeduplicationTests.cs`

### Implementation for User Story 1

- [X] T028 [P] [US1] Create Document aggregate root in `src/Modules/Documents/Documents.Domain/Aggregates/Document.cs` (AggregateRoot<DocumentId>, fields per data-model: TenantId, OrganizationId, OwnerId, Name, Classification*, RuleVersion, CurrentVersionId, Status, MimeType, Size, ContentHash, ProjectId, WorkItemId, Provenance, Retention*, IsSafe derived, ScanStatus, ScannedAt/By, RowVersion, methods Create, ChangeStatus, MarkSafe/MarkInfected, Delete, Approve, Reclassify, PublishNewVersion with CheckRule)
- [X] T029 [P] [US1] Create DocumentVersion aggregate root in `src/Modules/Documents/Documents.Domain/Aggregates/DocumentVersion.cs` (AggregateRoot<DocumentVersionId>, DocumentId, VersionNumber, ContentHash, MetadataSnapshot jsonb, MimeType, Size, IsPublished, PublishedAt/By, RuleVersion, IsSafe default false, ScanStatus Pending, ScannedAt/By, MarkSafe(idempotent), immutability guard via VersionIsImmutableOncePublishedRule)
- [X] T030 [P] [US1] Create DocumentProcessingJob aggregate root in `src/Modules/Documents/Documents.Domain/Aggregates/DocumentProcessingJob.cs` (AggregateRoot<DocumentProcessingJobId>, DocumentId, DocumentVersionId, TenantId, CurrentStage, StageStatusesJson dict, OverallStatus derived, AttemptCount, LastError, RuleVersion, transitions MarkSucceeded/MarkFailed/RetryStage with IBusinessRule)
- [X] T031 [P] [US1] Create DocumentAccessEntry entity in `src/Modules/Documents/Documents.Domain/Entities/DocumentAccessEntry.cs` (append-only, DocumentId, TenantId, ActorId, Action Read/Download/Denied, Granted bool, ClassificationValue, RuleVersion, Reason incl NotSafe, Timestamp, IpAddress, UserAgent)
- [X] T032 [P] [US1] Create DocumentExplicitGrant entity in `src/Modules/Documents/Documents.Domain/Entities/DocumentExplicitGrant.cs` (DocumentId, TenantId, GranteeUserId, GrantedBy, GrantedAt, ExpiresAt, RevokedAt, IsExpired)
- [X] T033 [P] [US1] Create domain service interfaces in `src/Modules/Documents/Documents.Domain/Services/IDocumentServices.cs` (IDocumentAccessPolicy, IClassificationPolicy, ISecurityScanProvider, IStorageGateway with PutAsync/GetAsync/ExistsAsync/CreatePresignedUrl + IsSafe gate)
- [X] T034 [US1] Implement IStorageGateway S3/MinIO adapter in `src/Modules/Documents/Documents.Infrastructure/Storage/S3StorageGateway.cs` (key sha256/{hash}.{ext}, ExistsAsync dedup check, PutAsync + re-hash SHA256 verification, GetAsync with IsSafe check returning Error.Forbidden if IsSafe=false, CreatePresignedUrl with IsSafe gate, AWSSDK.S3 + Minio SDK via config switch)
- [X] T035 [US1] Implement ISecurityScanProvider stub and adapter in `src/Modules/Documents/Documents.Infrastructure/Scanning/FakeSecurityScanProvider.cs` (ScanResult IsClean/Reason/Kind Clean|Infected|Unavailable, FakeSecurityScanProvider configurable per test hash/name, production ClamAV/ICAP adapter interface)
- [X] T036 [P] [US1] Implement UploadDocument vertical slice command/validator/handler in `src/Modules/Documents/Documents.Application/Features/UploadDocument/UploadDocumentCommand.cs` (ICommand<Result<UploadDocumentResponse>>, Validator name 1-300, Mime allow-list, size ≤100MB, tags 1-50 regex ^[a-z0-9_-]+$, customMetadata key ≤64/value ≤2KB/≤50 entries, project/workItem tenant-match via IProjectMembership stub)
- [X] T037 [US1] Implement UploadDocument handler persistence in `src/Modules/Documents/Documents.Application/Features/UploadDocument/UploadDocumentHandler.cs` (compute ContentHash.FromBytes, create Document+DocumentVersion+DocumentProcessingJob in one DocumentsDbContext transaction, stage via IOutboxWriter DocumentUploadedIntegrationEvent + DocumentProcessingStageRequestedIntegrationEvent Validation, no VirusScan/Classification in-request, persist to outbox_messages)
- [X] T038 [US1] Implement UploadDocument IEndpoint in `src/Modules/Documents/Documents.Application/Features/UploadDocument/UploadDocumentEndpoint.cs` (POST /api/documents multipart/form-data, Result→HTTP 202 with Location header, Idempotency-Key header support, TenantContext from JWT tenant_id)
- [X] T039 [US1] Implement GetDocument query with IsSafe exposure in `src/Modules/Documents/Documents.Application/Features/GetDocument/GetDocumentQuery.cs` (IQuery<Result<DocumentResponse>>, handler loads Document+current version+job, exposes isSafe/scanStatus/scannedAt, metadata-only DB verification - no blob bytes column assert, downloadUrl null when IsSafe=false or status not Available)
- [X] T040 [US1] Implement ListDocumentVersions query in `src/Modules/Documents/Documents.Application/Features/ListDocumentVersions/ListDocumentVersionsQuery.cs` (IQuery<Result<Paged<DocumentVersionResponse>>>, tenant-filtered, each version exposes isSafe/scanStatus, paginated page/pageSize)
- [X] T041 [US1] Implement GetDocument/ListVersions IEndpoints in `src/Modules/Documents/Documents.Application/Features/GetDocument/GetDocumentEndpoints.cs` (GET /api/documents/{id} and GET /api/documents/{id}/versions with authorization pre-filter placeholder - full policy in US3, but deny-by-default scaffold)
- [X] T042 [US1] Create EF migration for Documents schema initial tables in `src/Modules/Documents/Documents.Infrastructure/Persistence/Migrations/` (dotnet ef migrations add Documents_005_Initial --context DocumentsDbContext -- tables documents, document_versions, document_processing_jobs, document_access_entries, document_explicit_grants, classification_rules + outbox_messages, HasDefaultSchema documents)
- [X] T043 [US1] Wire RabbitMQ topic handlers skeleton for Validation/Metadata stages in `src/Modules/Documents/Documents.Infrastructure/Pipeline/ValidationHandler.cs` (IIntegrationEventHandler<DocumentProcessingStageRequestedIntegrationEvent> for Validation stage, idempotent guard if Succeeded return no-op, validate MIME/size/tenant/project link, mark Succeeded → publish next stage VirusScan via outbox, FailedRetryable on validation failure)

**Checkpoint**: Upload → Document+v1+Job persisted via outbox, binary deduplicated by hash in MinIO, HTTP <500ms with no pipeline sync execution; ListVersions returns v1 with snapshot

---

## Phase 4: User Story 2 - Immutable versions and lifecycle (corrections never mutate) (Priority: P1)

**Goal**: Every correction appends new immutable DocumentVersion (v1→v2 with new hash/snapshot/actor/time, prior unchanged, superseded event), mutation of published version rejected, deletion is soft audited lifecycle transition

**Independent Test**: Upload v1 → PublishDocumentVersion → v1 CurrentPointer=v1 with DocumentVersionPublished; PublishDocumentVersion with new bytes → v2 with new ContentHash/MetadataSnapshot, DocumentVersionSuperseded(v1) emitted, CurrentPointer=v2, GetDocumentVersion(1) unchanged reload; direct mutation of v1 hash throws VersionIsImmutableOncePublishedRule; DeleteDocument transitions status to Deleted, DocumentDeleted via outbox, binary denied except auditors

### Tests for User Story 2

- [X] T044 [P] [US2] Unit test for version immutability guard in `tests/Documents.Tests/Unit/VersionImmutabilityTests.cs` (load published v1, attempt set ContentHash/MetadataSnapshotJson → throws, reload equality unchanged)
- [X] T045 [P] [US2] Unit test for DocumentStatus lifecycle transition matrix in `tests/Documents.Tests/Unit/LifecycleTests.cs` (valid edges Draft→Uploaded→Validated→Classified→Indexed→Available→PendingApproval→Approved, *→ProcessingFailed, ProcessingFailed→Validated, Available→Deleted else Error.BusinessRule)
- [X] T046 [P] [US2] Unit test for MetadataSnapshot value-equality in `tests/Documents.Tests/Unit/MetadataSnapshotEqualityTests.cs` (all fields including ordered Tags/CustomMetadata)

### Implementation for User Story 2

- [X] T047 [P] [US2] Implement PublishDocumentVersion vertical slice validator in `src/Modules/Documents/Documents.Application/Features/PublishVersion/PublishDocumentVersionValidator.cs` (file optional if metadata-only, name 1-300, effective<=expiration, tag/custom bag invariants, expectedRowVersion base64 for optimistic concurrency)
- [X] T048 [US2] Implement PublishDocumentVersion handler in `src/Modules/Documents/Documents.Application/Features/PublishVersion/PublishDocumentVersionHandler.cs` (load Document+max VersionNumber, concurrency RowVersion check → 409 if stale, append new DocumentVersion VersionNumber=max+1, ContentHash new SHA256, MetadataSnapshot copied value, PublishedAt/By, RuleVersion current, Document.CurrentVersionId=newId, reset job for new version with Upload=Succeeded, Validation=Pending, emit DocumentVersionPublished + DocumentVersionSuperseded via outbox)
- [X] T049 [US2] Implement PublishDocumentVersion IEndpoint in `src/Modules/Documents/Documents.Application/Features/PublishVersion/PublishDocumentVersionEndpoint.cs` (POST /api/documents/{id}/versions multipart/form-data, 201 Created Location /api/documents/{id}/versions/{versionNumber}, 409 on RowVersion mismatch, 422 if status Deleted)
- [X] T050 [US2] Implement DeleteDocument vertical slice in `src/Modules/Documents/Documents.Application/Features/DeleteDocument/DeleteDocumentCommand.cs` (ICommand<Result>, Validator, Handler calls Document.Delete(actor) → DocumentStatusTransitionRule, sets DeletedAt/By, Status=Deleted, emits DocumentDeleted via outbox; soft delete never DELETE rows)
- [X] T051 [US2] Implement DeleteDocument IEndpoint in `src/Modules/Documents/Documents.Application/Features/DeleteDocument/DeleteDocumentEndpoint.cs` (DELETE /api/documents/{id}, 204 No Content, 422 illegal transition Already Deleted or Archived→Deleted invalid, 409 concurrency)
- [X] T052 [US2] Enforce immutability in DocumentVersion aggregate setters in `src/Modules/Documents/Documents.Domain/Aggregates/DocumentVersion.cs` (private setters throw VersionIsImmutableOncePublishedRule when IsPublished=true except MarkSafe Pending→Safe/Infected via MarkSafe method with audited DocumentVersionMarkedSafe event)
- [X] T053 [US2] Implement concurrency handling with RowVersion across mutating handlers in `src/Modules/Documents/Documents.Application/Common/ConcurrencyHelper.cs` (base64 expectedRowVersion parsing, EF RowVersion IsRowVersion() compare, map to Error.Concurrency → HTTP 409)
- [X] T054 [US2] Update GetDocument to return currentVersionNumber + handle Deleted visibility in `src/Modules/Documents/Documents.Application/Features/GetDocument/GetDocumentQuery.cs` (Deleted returns 404 shadow to non-auditors, auditors with document.audit.read see status=Deleted)

**Checkpoint**: v1 immutable after publish, v2 appended with superseded event, soft delete preserves rows, concurrency 409 on race, lifecycle matrix enforced via IBusinessRule

---

## Phase 5: User Story 3 - Classification-aware access evaluation and audited denials (Priority: P1)

**Goal**: Every read/download evaluated by pure IDocumentAccessPolicy (Golden Rule A + hierarchy + project membership + explicit grants + classification clearance + IsSafe antivirus gate deny-by-default, OR over grants, IsSafe=false denies even if Golden Rule A passes with reason=NotSafe), denials/ grants appended to DocumentAccessEntry and emitted via outbox

**Independent Test**: Seed Confidential doc owned by Alice Tenant T Project P (Bob not member, not in subtree, no grant) → GetDocument as Bob → 403/404 shadow, no binary, DocumentAccessDenied appended with reason NotInSubtreeOrMembership, auditor GetAccessHistory shows denial; Alice (owner)/Carol (subtree manager)/explicit grant holder with sufficient classification clearance on Safe doc → granted with DocumentAccessed + presigned URL; HighlyRestricted matrix 5 classifications × 8 actor types; IsSafe=false (ScanStatus Pending|Infected) → deny with reason NotSafe even if authorized, no blob served

### Tests for User Story 3

- [X] T055 [P] [US3] Unit test for Classification policy with org extensions in `tests/Documents.Tests/Unit/ClassificationPolicyTests.cs` (default 5 levels, org extension TopSecretFinance 101, unknown → Validation, ruleVersion stamp v3 vs v4, versioned rule table)
- [X] T056 [P] [US3] Unit test for IDocumentAccessPolicy matrix in `tests/Documents.Tests/Unit/AccessPolicyMatrixTests.cs` (5 classifications × 8 actor types = 40 cases × safe/unsafe = 80, plus tenant mismatch → 404, explicit grant, subtree OR membership, classification clearance denial)
- [X] T057 [P] [US3] Security matrix integration test per SPEC-013 in `tests/Documents.Tests/Security/DocumentAccessSecurityMatrixTests.cs` (HTTP-level GetDocument/Download denied/granted matrix with JWT stubs for owner/same-org peer/cross-org/subtree subordinate/explicit grant holder/auditor/anonymous)
- [X] T058 [P] [US3] Unit test for IsSafe gate in storage gateway in `tests/Documents.Tests/Unit/StorageIsSafeGateTests.cs` (IStorageGateway.GetAsync/CreatePresignedUrl denies when IsSafe=false even if policy mock would grant, audited NotSafe)

### Implementation for User Story 3

- [X] T059 [P] [US3] Implement ClassificationRule entity and seeder in `src/Modules/Documents/Documents.Infrastructure/Persistence/ClassificationRulesSeeder.cs` (seed Public|Internal|Confidential|Restricted|HighlyRestricted as ClassificationLevel Enumeration v1, per-org extensions TopSecretFinance 101 example, RuleSetJson jsonb with effectiveFrom, IsCurrent)
- [X] T060 [US3] Implement IClassificationPolicy service in `src/Modules/Documents/Documents.Infrastructure/Services/ClassificationPolicyService.cs` (ClassifyAsync(ctx→(Classification, ruleVersion)), AllowedLevels(orgId) via classification_rules IsCurrent, RuleSetJson deserialization, version stamp recorded on Document+DocumentVersion, fallback octet-stream warning, IMemoryCache keyed by orgId→ruleset, unknown → Error.Validation)
- [X] T061 [US3] Implement IDocumentAccessPolicy pure domain service in `src/Modules/Documents/Documents.Domain/Services/DocumentAccessPolicy.cs` (EvaluateAsync(AccessContext): 0 IsSafe==false → deny NotSafe, 1 tenant mismatch → deny 404, 2 owner → grant, 3 explicit grant → grant, 4 classification clearance actorMaxClassification(roles) vs classification Level ordered, 5 IManagementHierarchy.IsInSubtree(ownerId,actorId) → grant, 6 IProjectMembership.IsMember(projectId,actorId) → grant, steps 5/6 OR, deny-by-default, generic reason no detail leak)
- [X] T062 [US3] Implement IDocumentAccessPolicy infrastructure composition in `src/Modules/Documents/Documents.Infrastructure/Services/DocumentAccessPolicyService.cs` (adapter injecting IManagementHierarchy + IProjectMembership + explicit_grants read + IAuthorizationEvaluator permission map document.read/download, tenant-aware, pure logic delegates to domain policy)
- [X] T063 [US3] Update GetDocument handler to enforce full policy before fetch in `src/Modules/Documents/Documents.Application/Features/GetDocument/GetDocumentHandler.cs` (call IDocumentAccessPolicy.EvaluateAsync, on deny append DocumentAccessEntry granted=false reason incl NotSafe via same-tx outbox DocumentAccessDenied, map 404 shadow for cross-tenant, on grant append DocumentAccessEntry granted=true DocumentAccessed + return downloadUrl only if isSafe=true and status Available/Approved)
- [X] T064 [US3] Update ListDocumentVersions handler with same policy gate in `src/Modules/Documents/Documents.Application/Features/ListDocumentVersions/ListDocumentVersionsHandler.cs` (caller must be authorized for parent Document, each version exposes isSafe/scanStatus, filter via AuthorizedDocumentSpec)
- [X] T065 [US3] Update IStorageGateway to enforce IsSafe before blob access in `src/Modules/Documents/Documents.Infrastructure/Storage/S3StorageGateway.cs` (GetAsync/CreatePresignedUrl check DocumentVersion.IsSafe && ScanStatus==Safe, else return Error.Forbidden generic + emit NotSafe denial, never touch S3 bucket when not safe)
- [X] T066 [US3] Implement ClassifyDocument vertical slice (manual reclassification) in `src/Modules/Documents/Documents.Application/Features/ClassifyDocument/ClassifyDocumentCommand.cs` (ICommand<Result<DocumentResponse>>, Validator classification via AllowedLevels, Handler calls Document.Reclassify(newClassification, ruleVersion) → DocumentClassified via outbox, 409 concurrency, 422 if Deleted)
- [X] T067 [US3] Implement ClassifyDocument IEndpoint in `src/Modules/Documents/Documents.Application/Features/ClassifyDocument/ClassifyDocumentEndpoint.cs` (POST /api/documents/{id}/classify, Body {classification, reason, expectedRowVersion}, 200 with classification+ruleVersion+status Classified, 403 via policy clearance)
- [X] T068 [US3] Implement DownloadDocumentVersion query and handler in `src/Modules/Documents/Documents.Application/Features/DownloadDocument/DownloadDocumentQuery.cs` (DownloadDocumentQuery(documentId, versionNumber?), handler policy check incl IsSafe gate → deny NotSafe with audited entry, else IStorageGateway.GetAsync streaming with Content-Type MimeType + Content-Disposition + X-Content-Hash, presigned URL 302 alternative for classifications)
- [X] T069 [US3] Implement Download IEndpoint in `src/Modules/Documents/Documents.Application/Features/DownloadDocument/DownloadDocumentEndpoint.cs` (GET /api/documents/{id}/download/{versionNumber?}, 302 presigned or 200 stream, 403 NotSafe when IsSafe=false, 404 shadow when unauthorized, appends Download entry)
- [X] T070 [US3] Implement ExplicitGrant management helpers in `src/Modules/Documents/Documents.Infrastructure/Persistence/Repositories/ExplicitGrantRepository.cs` (GrantAccess DocumentExplicitGrant create, Revoke, IsGranted check with expiry RevokedAt IS NULL and unique DocumentId+GranteeUserId where RevokedAt IS NULL)

**Checkpoint**: Golden Rule A + classification + IsSafe gate deny-by-default with audited DocumentAccessDenied(DocumentAccessEntry) on every denial/grant, no binary served on deny or when IsSafe=false, org classification extensions evaluated via versioned ruleVersion stamp

---

## Phase 6: User Story 4 - Resumable processing job with explicit retryable failures (Priority: P2)

**Goal**: Upload pipeline Upload→Validation→VirusScan→Metadata→Classification→Storage→Indexing as DocumentProcessingJob with ProcessingStage Enumeration and per-stage Pending|InProgress|Succeeded|FailedRetryable|FailedPermanent, failures explicit retryable via RetryProcessingStage, VirusScan failure never leaves half-classified/Available, IsSafe marking controls readability

**Independent Test**: Upload → stub virus-scan to infected → assert job VirusScan=FailedRetryable DocumentProcessingFailed, DocumentStatus stays Processing/ProcessingFailed (not Available), isSafe=false, GetDocument shows processingStage + isSafe; RetryProcessingStage(jobId, VirusScan) with clean bytes → stage Succeeded, advances Metadata→Classification→Storage→Indexing each DocumentProcessingStageCompleted; GetDocument still blocks binary until IsSafe=true + Classification+Storage succeeded

### Tests for User Story 4

- [X] T071 [P] [US4] Unit test for ProcessingStage transitions in `tests/Documents.Tests/Unit/ProcessingStageTests.cs` (Upload→Validation→VirusScan→Metadata→Classification→Storage→Indexing order, per-stage Pending→InProgress→Succeeded/FailedRetryable, maxAttempts 3 → FailedPermanent)
- [X] T072 [P] [US4] Integration test for pipeline with MinIO+scan stub in `tests/Documents.Tests/Integration/PipelineIntegrationTests.cs` (outbox-driven stages against real Npgsql+MinIO+FakeScanProvider, clean path completes <10s, infected path stays FailedRetryable, retry advances)
- [X] T073 [P] [US4] Integration test for retry idempotency in `tests/Documents.Tests/Integration/RetryTests.cs` (re-executing Succeeded stage no-op, FailedRetryable retry increments AttemptCount, exponential backoff 2^attempt*500ms, maxAttempts→FailedPermanent)
- [X] T074 [P] [US4] Integration test for hash verification at Storage stage in `tests/Documents.Tests/Integration/HashVerificationTests.cs` (mismatched ContentHash → FailedRetryable HashMismatch, deduplication key prevents duplicate blobs)

### Implementation for User Story 4

- [X] T075 [US4] Implement VirusScan pipeline handler in `src/Modules/Documents/Documents.Infrastructure/Pipeline/VirusScanHandler.cs` (IIntegrationEventHandler<DocumentProcessingStageRequestedIntegrationEvent> for VirusScan, call ISecurityScanProvider.ScanAsync staged bytes, Clean → DocumentVersion.MarkSafe IsSafe=true ScanStatus=Safe ScannedAt/By + job MarkSucceeded → publish Metadata event via outbox, Infected/Unavailable → IsSafe=false ScanStatus Infected/Unavailable + job MarkFailed FailedRetryable(reason Infected/ScannerUnavailable) + DocumentProcessingFailed + DocumentStatus ProcessingFailed, nunca procede a Storage, idempotent guard)
- [X] T076 [US4] Implement Metadata pipeline handler in `src/Modules/Documents/Documents.Infrastructure/Pipeline/MetadataHandler.cs` (extract author/dept/tags/type/dates/source/confidentiality/retention/custom bag into MetadataSnapshot VO, store as jsonb on DocumentVersion, isSafe must be true precondition else skip, FailedRetryable on extraction failure)
- [X] T077 [US4] Implement Classification pipeline handler in `src/Modules/Documents/Documents.Infrastructure/Pipeline/ClassificationHandler.cs` (call IClassificationPolicy.ClassifyAsync at execution time (not enqueue), resolve final Classification + ruleVersion, stamp on Document+DocumentVersion, emit DocumentClassified via outbox, advance to Storage; re-classify on retry uses current rule version)
- [X] T078 [US4] Implement Storage pipeline handler in `src/Modules/Documents/Documents.Infrastructure/Pipeline/StorageHandler.cs` (call IStorageGateway.PutAsync bytes with ContentHash, ExistsAsync dedup idempotent check, SHA-256 re-hash verification → mismatch FailedRetryable HashMismatch, success MarkSucceeded)
- [X] T079 [US4] Implement Indexing pipeline handler in `src/Modules/Documents/Documents.Infrastructure/Pipeline/IndexingHandler.cs` (publish DocumentIndexedIntegrationEvent for BC-07 Search with ContentHash+MimeType+MetadataSnapshotJson+Classification+RuleVersion+TenantId, duplicate publish idempotent via EventId, terminal Succeeded → job CompletedAt)
- [X] T080 [US4] Update ValidationHandler to publish next stage after success in `src/Modules/Documents/Documents.Infrastructure/Pipeline/ValidationHandler.cs` (on Succeeded publish VirusScan event, full async chain Upload→…→Indexing via outbox+RabbitMQ topic document.processing.*, manual ack + publisher confirms, same CorrelationId/TenantContext OTel baggage documentId/versionId/stage)
- [X] T081 [US4] Implement GetProcessingJob query and handler in `src/Modules/Documents/Documents.Application/Features/ProcessingJobs/GetProcessingJobQuery.cs` (GetProcessingJobQuery(documentId, versionNumber?), handler loads job+version, returns overallStatus currentStage stages map AttemptCount LastError RuleVersion, inherits document authorization via IDocumentAccessPolicy Read, denials append DocumentAccessDenied)
- [X] T082 [US4] Implement GetProcessingJob IEndpoint in `src/Modules/Documents/Documents.Application/Features/ProcessingJobs/GetProcessingJobEndpoint.cs` (GET /api/documents/{id}/processing?versionNumber=, 200 with stages Pending/Succeeded/FailedRetryable, Document.status Available only when Storage==Succeeded && Classification==Succeeded invariant documented)
- [X] T083 [US4] Implement RetryProcessingStage vertical slice in `src/Modules/Documents/Documents.Application/Features/ProcessingJobs/RetryProcessingStageCommand.cs` (ICommand<Result<ProcessingJobResponse>>, Validator stage known, Handler DocumentProcessingJob.RetryStage(stage, actor) → CheckRule StageIsRetryableRule (Succeed→422 Already succeeded, overall Succeeded→422), reset StageStatuses[stage]=Pending increment AttemptCount clear LastError, permissioned via IDocumentAccessPolicy processing.retry, publish StageRequested event via outbox, 400 unknown stage, 409 concurrency on RowVersion)
- [X] T084 [US4] Implement RetryProcessingStage IEndpoint in `src/Modules/Documents/Documents.Application/Features/ProcessingJobs/RetryProcessingStageEndpoint.cs` (POST /api/documents/{id}/processing/retry Body {stage, versionNumber, expectedRowVersion}, 200 with newStatus Pending retryAttempt, after maxAttempts→FailedPermanent returns 422 unless admin reset, 403 audited)
- [X] T085 [US4] Wire RabbitMQ topics and OutboxProcessor integration in `src/Modules/Documents/Documents.Infrastructure/Pipeline/PipelineEventBusConfiguration.cs` (register handlers for document.processing.validation/virusscan/metadata/classification/storage/indexing, topic exchange with manual ack, exponential backoff 2^attempt*500ms capped 30s, idempotent via EventId dedup table outbox_consumed_events or job+stage key, out-of-order delivery re-queue)
- [X] T086 [US4] Enforce IsSafe gate in pipeline ordering invariant in `src/Modules/Documents/Documents.Infrastructure/Pipeline/PipelineOrderingGuard.cs` (Classification and Storage stages verify prior VirusScan IsSafe=true precondition, otherwise skip/return FailedRetryable NotSafe, GetDocument shows indexingState NotIndexed until pipeline complete, no half-classified document visible as Available)

**Checkpoint**: Job with per-stage status observable via query, virus failure explicit retryable with IsSafe=false blocking container reads, retry idempotent with maxAttempts=3 before FailedPermanent, no half-classified Available, full chain verified by integration tests with MinIO+scan stub

---

## Phase 7: User Story 5 - Auditor access history and approver flow (Priority: P2)

**Goal**: Auditor/owner can query GetAccessHistory(documentId) → reads+denials+downloads with actor/timestamp/classification/ruleVersion chronologically; non-auditor/non-owner denied with audited HistoryAccessDenied; approver runs ApproveDocument with lifecycle guard PendingApproval→Approved emitting DocumentApproved via outbox

**Independent Test**: Perform owner reads×2, Bob denials×2, owner download×1 on doc → GetAccessHistory as auditor/owner returns 5 entries with actor/action/timestamp/classification/granted ordered ASC, paginated; Bob GetAccessHistory → 403 Forbidden + audited denial; ApproveDocument by authorized approver transitions PendingApproval→Approved with DocumentApproved, second approve on Approved/Deleted → 422 BusinessRule illegal transition; ruleVersion v3 visible on doc survives rule advance to v4

### Tests for User Story 5

- [X] T087 [P] [US5] Integration test for auditor history retrieval in `tests/Documents.Tests/Integration/AccessHistoryTests.cs` (seed 2 reads+1 download+2 denials, GetAccessHistory as auditor/owner returns 5 chronologically with pagination, non-auditor gets 403, history-query denial itself appended as Denied HistoryAccessDenied)
- [X] T088 [P] [US5] Unit test for approval lifecycle guard in `tests/Documents.Tests/Unit/ApprovalLifecycleTests.cs` (PendingApproval→Approved valid emits DocumentApproved, Approved→Approved and Deleted→Approved rejected Error.BusinessRule, approver must satisfy IDocumentAccessPolicy approve permission)

### Implementation for User Story 5

- [X] T089 [US5] Implement GetAccessHistory query validator in `src/Modules/Documents/Documents.Application/Features/AccessHistory/GetAccessHistoryQuery.cs` (GetAccessHistoryQuery(documentId, page=1, pageSize=50, action? Read|Download|Denied) : IQuery<Result<Paged<DocumentAccessEntryResponse>>>, Validator page>=1 pageSize 1-100 action enum, scope check actor==Document.OwnerId OR IAuthorizationEvaluator.CanActorPerform(actor, "document.audit.read") else Error.Forbidden)
- [X] T090 [US5] Implement GetAccessHistory handler in `src/Modules/Documents/Documents.Application/Features/AccessHistory/GetAccessHistoryHandler.cs` (check tenant DocumentByTenantSpec, scope gate, history-query denial appends DocumentAccessEntry Action=Denied Reason=HistoryAccessDenied audit, success queries document_access_entries WHERE DocumentId+TenantId ORDER BY Timestamp ASC paginated, includes classification+ruleVersion at time of access)
- [X] T091 [US5] Implement GetAccessHistory IEndpoint in `src/Modules/Documents/Documents.Application/Features/AccessHistory/GetAccessHistoryEndpoint.cs` (GET /api/documents/{id}/history?page&pageSize&action, 200 envelope {items,totalCount,page,pageSize}, 403 if not auditor/owner, 404 tenant shadow, 400 pagination validation)
- [X] T092 [US5] Implement ApproveDocument vertical slice in `src/Modules/Documents/Documents.Application/Features/ApproveDocument/ApproveDocumentCommand.cs` (ICommand<Result<DocumentResponse>>, Validator expectedRowVersion, Handler checks IDocumentAccessPolicy approve permission (role+subtree+classification clearance), calls Document.Approve(actor) → CheckRule DocumentStatusTransitionRule PendingApproval→Approved valid else Error.BusinessRule, sets ApprovedAt/By, emits DocumentApproved via outbox, 409 concurrency)
- [X] T093 [US5] Implement ApproveDocument IEndpoint in `src/Modules/Documents/Documents.Application/Features/ApproveDocument/ApproveDocumentEndpoint.cs` (POST /api/documents/{id}/approve Body {expectedRowVersion}, 200 {status Approved approvedAt approvedBy}, 422 illegal transition, 403 clearance, 409 concurrency)
- [X] T094 [US5] Ensure DocumentStatus Available/Approved/Deleted lifecycle wiring covers retention expiry in `src/Modules/Documents/Documents.Domain/Aggregates/Document.cs` (IsExpired query-time derived via RetentionPolicy.IsExpired(now), flag RetentionExpired status transition not auto-delete, requires explicit Archive/Delete lifecycle action)

**Checkpoint**: Auditor GetAccessHistory returns all access types chronologically with ruleVersion stamp surviving policy changes, non-auditor history access audited as denial, approval gate enforces lifecycle with outbox event

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, observability, and validation that affects multiple stories

- [X] T095 [P] Add OTel tracing baggage for document handlers in `src/Modules/Documents/Documents.Infrastructure/Observability/DocumentTracingEnricher.cs` (documentId/versionId/stage baggage, correlationId propagation via TenantContext, audit correlation)
- [X] T096 [P] Implement health check for DocumentsDbContext and S3 gateway in `src/Api/Features/GetPlatformHealth/DocumentsHealthCheck.cs` (check postgres schema documents, MinIO/S3 bucket reachability, scan provider stub health)
- [X] T097 [P] Harden authorization error mapping to generic messages in `src/Modules/Documents/Documents.Application/Common/AuthorizationErrorMapper.cs` (deny returns generic Error.Forbidden no reason leak, cross-tenant returns 404 not 403 to avoid enumeration, NotSafe mapped to 403 with generic message)
- [X] T098 [P] Add rate limiting for UploadDocument per actor/tenant in `src/Api/Configuration/RateLimitingConfiguration.cs` (existing middleware from 002, add policy document.upload per actor/tenant sliding window)
- [X] T099 [P] Write quickstart validation script and run quickstart.md pillars SC-001..008 in `specs/005-document-management/quickstart-validation.sh` (bash script executing time curl upload, dedup, version publish, access matrix, virus retry, rule version, history, approval)
- [X] T100 [P] Add retention expiry query helper and index for effectiveDate in `src/Modules/Documents/Documents.Infrastructure/Specifications/RetentionSpecifications.cs` (RetentionExpired flag query WHERE expirationDate < now AND !legalHold, index on MetadataEffectiveDate)
- [X] T101 [P] Perform performance validation for GetDocument <150ms and GetAccessHistory 1k entries <300ms paginated in `tests/Documents.Tests/Performance/DocumentPerformanceTests.cs`
- [X] T102 Run full suite: `dotnet build OroKanban.slnx -warnaserror` and `dotnet test tests/Documents.Tests -v minimal && dotnet test tests/Architecture -v minimal` and verify no cross-module Infra refs violation

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately (AppHost MinIO + Api wiring + test project)
- **Foundational (Phase 2)**: Depends on Setup - BLOCKS all user stories (VOs, Enumerations, Rules, Specs, EF configs, Contracts)
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - US1 (Upload pipeline) can start first - no story dependencies, creates Document/Version/Job aggregates
  - US2 (Versions/Lifecycle) depends on US1 Document aggregate + PublishNewVersion logic
  - US3 (Classification-aware access + IsSafe gate) depends on US1 Document aggregate + needs US2 RowVersion/immutability but logically parallelizable after US1; classification policy and access policy compose hierarchy/membership
  - US4 (Resumable job retries) depends on US1 Job + US3 IsSafe gate, extends pipeline handlers
  - US5 (Auditor history/approve) depends on US3 access history entries + US2 lifecycle for approval transitions
- **Polish (Phase 8)**: Depends on all desired user stories complete

### User Story Dependencies

- **US1 (P1) Upload**: Can start after Foundational - No other story dependencies - Produces Document/Version/Job/Storage
- **US2 (P1) Versions**: Requires US1 Document aggregate (T028) and Job (T030) to extend with Publish/Delete; can be staffed in parallel after T028-T030 done
- **US3 (P1) Access**: Requires US1 Document + US2 immutability but policy service (T061) is pure and parallelizable; storage IsSafe gate (T065) touches same file as T034 so sequence with T034
- **US4 (P2) Processing jobs**: Requires US1 Upload handler (T037) + US3 VirusScan IsSafe (T075 depends on T061 gate) - builds full stage chain
- **US5 (P2) History/Approve**: Requires US3 Access entries (T063) and US2 lifecycle (T052) - reads same DocumentAccessEntry table US3 writes

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation (TDD)
- Aggregates/VOs before domain services
- Domain services before application handlers
- Handlers before IEndpoints
- Core implementation before integration (e.g., UploadDocument handler before ValidationHandler chain)
- Story complete before moving to next priority for MVP incremental delivery

### Parallel Opportunities

- All Setup tasks marked [P] (T005-T006) can run in parallel
- All Foundational tasks marked [P] (T008-T017, T019) can run in parallel within Phase 2 (different VO/enumeration files)
- Foundational configs T020-T022 can be parallel after T007 done
- US1 domain aggregates T028-T033 can be parallel (different files)
- US1 handlers T036-T038 sequential (same slice folder - avoid conflict)
- US tests within same phase marked [P] can run in parallel (different test files)
- Once Foundational completes, US1 can start; after T028-T030, US2 and US3 policy pure units can be staffed in parallel by different devs (US3 T059-T061 pure work doesn't touch US2 version files)
- US4 handlers T075-T079 can be parallelized per stage file (different handlers)
- Polish tasks marked [P] (T095-T101) can run in parallel (different files)

---

## Parallel Example: User Story 1

```bash
# Domain models in parallel (different files):
Task: "Create Document aggregate in src/Modules/Documents/Documents.Domain/Aggregates/Document.cs" (T028)
Task: "Create DocumentVersion aggregate in src/Modules/Documents/Documents.Domain/Aggregates/DocumentVersion.cs" (T029)
Task: "Create DocumentProcessingJob aggregate in src/Modules/Documents/Documents.Domain/Aggregates/DocumentProcessingJob.cs" (T030)
Task: "Create DocumentAccessEntry entity in src/Modules/Documents/Documents.Domain/Entities/DocumentAccessEntry.cs" (T031)

# Tests in parallel:
Task: "Unit test for VOs in tests/Documents.Tests/Unit/ValueObjectsTests.cs" (T025)
Task: "Integration test for Upload pipeline in tests/Documents.Tests/Integration/UploadPipelineTests.cs" (T026)

# Handlers are sequential within slice folder (shared file concerns):
# T036 (Validator) → T037 (Handler) → T038 (Endpoint)
```

## Parallel Example: User Story 3 (access policy)

```bash
# Pure domain + infra policy in parallel with version work (after Foundational):
Task: "ClassificationRule seeder in ClassificationRulesSeeder.cs" (T059) # different file than US2 T047
Task: "ClassificationPolicy service in ClassificationPolicyService.cs" (T060)
Task: "AccessPolicy pure domain in DocumentAccessPolicy.cs" (T061)
Task: "AccessPolicy integration tests matrix in DocumentAccessSecurityMatrixTests.cs" (T057)
```

---

## Implementation Strategy

### MVP First (User Stories 1 + 2 + 3 = Core Security MVP)

Because three P1 stories form the inseparable security core (no document without upload, no trust without immutability, no safety without classification+IsSafe), MVP is US1→US2→US3 sequentially:

1. Complete Phase 1: Setup (T001-T006) - AppHost MinIO + Api wiring
2. Complete Phase 2: Foundational (T007-T024) - VOs/Enumerations/Rules/Configs/Contracts
3. Complete Phase 3: User Story 1 (T025-T043) - Upload + outbox + S3 dedup + GetDocument/ListVersions
4. **STOP and VALIDATE**: time curl UploadDocument <500ms, MinIO blob exists, job queued, no sync scan (SC-001)
5. Complete Phase 4: User Story 2 (T044-T054) - PublishVersion, immutability guard, soft Delete, concurrency 409 (SC-002)
6. **STOP and VALIDATE**: v2 appended, v1 unchanged, direct mutation throws
7. Complete Phase 5: User Story 3 (T055-T070) - ClassificationPolicy + AccessPolicy + IsSafe gate + Classify/Download
8. **STOP and VALIDATE MVP**: Security matrix 5×8 passes, IsSafe=false denies with NotSafe, no binary served on deny (SC-003 + SC-008)
9. Deploy/demo MVP at this point: document lifecycle with classification-aware, virus-gated access is complete and audited

### Incremental Delivery (Full Feature)

1. Setup + Foundational → Foundation ready (T001-T024)
2. Add US1 → Test independently → Deploy/Demo (upload + pipeline + storage)
3. Add US2 → Test independently → Deploy/Demo (+ immutability + lifecycle + soft delete)
4. Add US3 → Test independently → Deploy/Demo MVP (+ classification + IsSafe gate + matrix)
5. Add US4 → Test independently → Deploy/Demo (+ retryable stages VirusScan→Indexing via RabbitMQ, SC-004)
6. Add US5 → Test independently → Deploy/Demo (+ auditor history + approval gates SC-006/SC-007)
7. Polish → Cross-cutting OTel/health/rate-limiting/perf (SC-005 ruleVersion stamp survives across all)

### Parallel Team Strategy

With 3 developers after Foundational:

- **Developer A**: US1 (UploadDocument slices) → US2 (PublishVersion/DeleteDocument) - owns DocumentVersion lifecycle
- **Developer B**: US3 (ClassificationPolicy + DocumentAccessPolicy + IsSafe gate) - owns security matrix and storage gate, pure/testable isolation allows early start after T028
- **Developer C**: US4 (Pipeline handlers VirusScan→Indexing + RetryProcessingStage) - owns Job aggregate and RabbitMQ topics, coordinates with B on IsSafe gate; then joins A or B for US5 history/approve

Stories integrate via DocumentId/VersionId FK and outbox topics; no cross-story file conflicts except shared aggregates (coordinate via row-level ownership).

---

## Notes

- [P] tasks = different files, no dependencies - safe to parallelize
- [Story] label maps task to user story for traceability (FR-001..022 coverage documented in task descriptions)
- Each user story independently completable and testable with its Independent Test criteria
- Verify tests fail before implementing (TDD per Constitution XXI)
- Commit after each task or logical group (e.g., T028-T033 domain batch)
- Stop at any checkpoint to validate story independently (quickstart.md pillars SC-001..008 map to story checkpoints)
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence
- Tenant isolation: every Specification includes tenant_id, cross-tenant returns 404 (not 403) per Principle XV
- Outbox: every business write/deny/access/approval/delete uses same-transaction IOutboxWriter, never loses audit
- S3: metadata-only DB (no bytea), blob keyed by ContentHash hex, hash verification at Storage stage, presigned URL/stream only when isSafe=true

