# Implementation Plan: Document Management

**Branch**: `005-document-management` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md) | **Depends on**: 002-identity-access-organization (IManagementHierarchy, IAuthorizationEvaluator, TenantContext) + 003-projects-work-kanban (Project, WorkItem, ProjectMember, RowVersion)

**Input**: Feature specification — BC-05 Documents (Core). R1 Document aggregate with classification + tenant/owner/project links, R2 immutable DocumentVersion + soft delete lifecycle, R3 MetadataSnapshot VO with snapshot semantics, R4 Golden Rule A + classification + seguridad sanitaria (`IsSafe` antivirus) access via IDocumentAccessPolicy with audited denials + access history, R5 async outbox-driven pipeline Upload→Validation→VirusScan→Metadata→Classification→Storage→Indexing with ProcessingStage Enumeration, retryable failures y marcado seguro `IsSafe`/`ScanStatus`, R6 hash-referenced S3-compatible object storage with metadata-only DB y bloqueo de lectura hasta `IsSafe=true` + SPEC-012 at-rest protection. **Clarification 2026-09-01**: todo documento/versión debe ser escaneado y marcado seguro; contenedor no sirve lectura/descarga si `IsSafe=false`.

## Summary

Implement BC-05 as the single bounded context that owns enterprise documents as first-class aggregates. `Document` and `DocumentVersion` persist in `documents` schema (one logical PostgreSQL via Aspire `postgres`, tenant-scoped, RowVersion concurrency, deduplication by `ContentHash`, **por-versión `IsSafe`/`ScanStatus`/`ScannedAt` marcado por etapa `VirusScan`**), `MetadataSnapshot` is a ValueObject copied at publish, `Classification` is a ValueObject validated by versioned `IClassificationPolicy` (default `Public..HighlyRestricted` + org extensions, ruleVersion stamped), `DocumentStatus` and `ProcessingStage` are `Enumeration` with lifecycle/`IBusinessRule` guards, access is deny-by-default via pure `IDocumentAccessPolicy` composing `IManagementHierarchy` + `IProjectMembership` + explicit grants + classification clearance **+ `IsSafe` check (bloqueo por defecto hasta `ScanStatus=Safe`; `NotSafe` → `DocumentAccessDenied` aunque Golden Rule A pase)** with every denial/grant appended to `DocumentAccessEntry` and emitted via outbox, the upload pipeline is an outbox-driven `DocumentProcessingJob` aggregate with per-stage status and `RetryProcessingStage` idempotent handlers (BuildingBlocks EventBus.RabbitMQ topic `document.processing.*`) donde `VirusScan` → `MarkSafe` (`IsSafe=true`) o `FailedRetryable` (`IsSafe=false`), binary bytes live only in S3-compatible object storage (MinIO in dev via Aspire, AWS S3 in prod) keyed by SHA-256 `ContentHash` **con `IStorageGateway` verificando `IsSafe` antes de presigned URL/stream** + hash verification at Storage stage, and the public surface is vertical-slice `IEndpoint` + `Result→HTTP` contracts for `UploadDocument`/`PublishDocumentVersion`/`ClassifyDocument`/`ApproveDocument`/`RetryProcessingStage`/`DeleteDocument` + filtered `GetDocument`/`ListDocumentVersions` (`isSafe`/`scanStatus` expuestos) /`GetAccessHistory`, all authorization-filtered (+ `IsSafe` gate) before fetch.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript Angular latest (document UI per `minimal-ui-design-system` skill; read contracts only)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, Enumeration, ValueObject, IBusinessRule/CheckRule, Specification<T>, Result/Error, IRepository), `BuildingBlocks.CQRS` (ISender, ICommand/IQuery, ICommandHandler/IQueryHandler, IPipelineBehavior — Validation + Logging), `BuildingBlocks.EventBus` + `RabbitMQ` (IntegrationEvent, IEventBus, outbox), `BuildingBlocks.ServiceDefaults` (already wired — OTel/Serilog/health/resilience), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, OutboxEntityTypeConfiguration, UnitOfWork), `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore` (HasDefaultSchema, RowVersion), `AWSSDK.S3` + `AWSSDK.Extensions.NETCore.Setup` or `Minio` SDK (S3-compatible storage; resolved via ADR-005-01), `Microsoft.AspNetCore.Authentication.JwtBearer` (already in Api — provides `sub`/`tenant_id`/roles), `StackExchange.Redis` via Aspire `redis` (optional for classification rule cache; IDocumentAccessPolicy remains pure, no mandatory cache)

**Storage**: PostgreSQL via Aspire `postgres` — schema `documents` (via `HasDefaultSchema("documents")`). Tables `documents.documents`, `documents.document_versions`, `documents.document_processing_jobs`, `documents.document_access_entries`, `documents.classification_rules` (versioned), `documents.document_explicit_grants`, `outbox_messages`. Binary bytes in S3-compatible object storage (MinIO container in dev via Aspire `AddMinio` or `AddAwsS3` stub in AppHost; AWS S3 bucket in prod) keyed by `ContentHash` SHA-256 hex. Redis via Aspire `redis` optionally for `IClassificationPolicy` ruleVersion cache. Outbox per `AppDbContextBase`.

**Testing**: xUnit (`dotnet test`), NetArchTest, Testcontainers for Postgres + MinIO (via `Testcontainers.Minio`) + optional local S3 mock, `NSubstitute` for `IManagementHierarchy`/`ISecurityScanProvider`/`IStorageGateway` fakes, `Microsoft.AspNetCore.TestHost` for Api auth filtering. TDD: unit (Version immutability, Classification policy incl. org extensions, access policy matrix, lifecycle legality, MetadataSnapshot equality, ProcessingStage transitions), integration (outbox-driven pipeline against real Npgsql + MinIO + scan stub, retry semantics, deduplication, hash verification), security matrix (every classification × every SPEC-013 actor type), E2E (upload→pipeline→classified→available→auditor history).

**Target Platform**: Linux containers via Podman (Aspire dashboard), `oroidentityserver` external container reference already declared in `OroKanban.AppHost/AppHost.cs` (Authority via `Identity__Authority` / `Oidc__Authority`). Api is the single composition host exposing `src/Modules/Documents` endpoints via vertical slices.

**Project Type**: Modular monolith — this feature touches `src/Modules/Documents` (new aggregates/domain services/vertical slices) and consumes `src/Modules/Organization` + `src/Modules/Projects` + `src/Modules/Identity` via Shared Kernel contracts plus `src/Api` wiring and `src/Web` document components.

**Performance Goals**: `UploadDocument` HTTP acceptance <500 ms p95 (no pipeline stage runs in-request; SC-001); `GetDocument` with access evaluation <150 ms p95; `ClassifyDocument`/`PublishDocumentVersion` <200 ms; `GetAccessHistory` for 1k entries <300 ms p95 paginated; pipeline stage retry round-trip <2 s with exponential backoff; 99% virus-scan clean path Upload→Available <10 s in dev (scan stub instant).

**Constraints**: Principle I: reuse BuildingBlocks canon — no MediatR/MassTransit/AutoMapper; Principle VI: rules in Domain via `CheckRule`/`IBusinessRule`/`Specification<T>` — controllers never mutate Version immutability; Principle VII: unbounded hierarchy, classification-aware, every read filtered before fetch; Principle VIII: append-only audit via outbox for every business write/deny/access/approval/delete; Principle IX: immutable published Version, soft delete only; Principle XV: every `Specification<T>` includes `tenant_id`, cross-tenant access returns 404 (not 403) to avoid enumeration; Principle XVII: async via outbox + RabbitMQ, handlers idempotent, no stage blocks HTTP after Upload; Principle XIX: deny-by-default, least privilege, protected storage per SPEC-012, no bucket enumeration; FR: any new project/file via platform CLIs (`dotnet new classlib` for new slice folders) not manual copy; S3 selection via ADR.

**Scale/Scope**: 3 aggregates (Document, DocumentVersion, DocumentProcessingJob) + 7 VOs/Enumerations (Classification, ContentHash, MimeType, RetentionPolicy, DocumentStatus, ProcessingStage, MetadataSnapshot) + 1 append entity (DocumentAccessEntry) + 2 domain services (IDocumentAccessPolicy, IClassificationPolicy) + ~6 commands + 3 queries (vertical slices), ~45 new files in Documents module; seeded enumerations: 5 default Classifications + per-org extensions; no new Aspire resources beyond optional MinIO (dev) — reuses postgres/redis/rabbitmq.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` canon (AggregateRoot, StronglyTypedId, Enumeration, ValueObject, IBusinessRule, Specification, Result, ISender, AppDbContextBase, EfRepository, outbox, IEndpoint, Result→HTTP) and `.agents/skills/ddd-project-planner` + `minimal-ui-design-system` + `ngrx-signal-store` mandates; no new ORM/event bus — Npgsql/Redis/RabbitMQ already in 002. BuildingBlocks direct reuse.
- [x] **II — oroidentityserver Mandatory**: Consumed only — `sub`/`tenant_id`/roles from JWT validated via discovery; `UploadDocument`/`GetDocument` propagate `TenantContext` (from 002) as first gate. No local login.
- [x] **III — .NET 10**: All code targets `net10.0`.
- [x] **IV — Aspire Orchestrator**: Adds an S3-compatible resource to existing AppHost (`postgres`/`redis`/`rabbitmq` already declared); `identity-api` remains external container reference; no duplication of identity infrastructure. MinIO in dev, AWS S3 in prod via configuration — AppHost `WithReference` pattern.
- [x] **V — Modular Architecture**: BC-05 owns `Documents` module; cross-module only via `Organization.Contracts` (`IManagementHierarchy`, `IAuthorizationEvaluator`) + `Projects.Contracts` (`IProjectMembership`) and EventBus integration events (`DocumentUploaded`→search/indexing, `DocumentApproved`→notifications). No direct DbContext cross-reference — enforced by Architecture test.
- [x] **VI — Domain Rules Belong to the Domain**: `VersionIsImmutableOncePublishedRule`, `DocumentStatusTransitionRule`, `ClassificationIsValidRule`, `MetadataSnapshotValidationRule`, `AccessAllowedRule` are `IBusinessRule` via `CheckRule` in Domain; `IDocumentAccessPolicy`/`IClassificationPolicy` are pure domain services, not controllers. UI never mutates versions.
- [x] **VII — Hierarchical Authorization**: Every `GetDocument`/`ListDocumentVersions` composes subtree + project-membership + explicit-grant + classification `Specification<T>` before fetch; unbounded depth via `IManagementHierarchy`; explicit `DocumentAccessDenied` entries. Dedicated authorization tests per SPEC-013 matrix (every classification × every actor type).
- [x] **VIII — Everything Important Is Auditable**: Upload, classification, version publish/supersede, access/denied, delete, approval, processing stage completed/failed all emit append-only via same-transaction outbox; `DocumentAccessEntry` is append-only; updates never mutate history.
- [x] **IX — Documents Are First-Class Domain Objects**: `Document` has identity, classification, owner, tenant, project/work-item links, current version pointer, MIME/size/hash, DocumentStatus lifecycle, provenance, retention, access history; `DocumentVersion` immutable once published; new version on correction; soft audited delete.
- [x] **XV — Tenant/Organization Aware**: Every `Specification<T>` includes `tenant_id`; `TenantContext` is first predicate in `IDocumentAccessPolicy`; search results authorization-filtered. Cross-tenant returns 404.
- [x] **XVI — APIs Are Contracts**: Stable request/response DTOs per slice, pagination/filtering/sorting, `CurrentVersion` + `RowVersion` concurrency via `If-Match`/body `expectedRowVersion`, `Result→HTTP` mapping (400 validation, 403 generic denial, 404 tenant-aware, 409 concurrency via `Error.Conflict`), OpenAPI via Aspire.
- [x] **XVII — Async Preferred**: Long operations (scan/classification/indexing) via outbox→EventBus; notification/search integration events are outbox-published; handlers idempotent with manual ack + exponential retries (at-least-once).
- [x] **XVIII — Observability Mandatory**: `AddServiceDefaults()` OTel flow; document handlers traced with `documentId`/`versionId`/`stage` baggage; audit correlated via `correlationId`; health via `/health`/`/alive`.
- [x] **XIX — Security by Default**: Deny-by-default, least privilege, generic deny message (no reason leak), no bucket enumeration, presigned-URL/streaming gateway, input validation via `Validator<T>` (Golden Rule B), protected storage per SPEC-012, SHA-256 hash verification.
- [x] **XX — Testability Is Architectural**: Unit (immutability, classification extensions, access matrix, lifecycle, snapshot equality, stage transitions), integration (outbox pipeline with real MinIO + Npgsql + scan stub, retry, dedup), security matrix (classification × actor), E2E (upload→pipeline→history) — all required.
- [x] **XXI — TDD+DDD+Vertical Slices**: Aggregates as `AggregateRoot<StronglyTypedId>`, slices as `ICommand`/`IQuery`+`Validator`+`Handler`+`IEndpoint`, manual mapping, `Result`/`Error`, `Specification<T>` for filtered queries, own `ISender`.
- [x] **XXII — Skills Govern Design**: `ddd-project-planner` bounded context BC-05, `minimal-ui-design-system` tokens/elevation for document list/detail/history UI, `ngrx-signal-store` for document SignalStore (no contradiction — backend is pure DDD).

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 expected to remain PASS (Phase 1 adds only documentation; no new gates introduced).

## Project Structure

### Documentation (this feature)

```text
specs/005-document-management/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── documents-api-contract.md        # UploadDocument, PublishDocumentVersion, ClassifyDocument, ApproveDocument, DeleteDocument + GetDocument/ListVersions with auth filtering
│   ├── processing-jobs-contract.md      # DocumentProcessingJob pipeline, RetryProcessingStage, stage status, job queries
│   ├── access-history-contract.md       # GetAccessHistory (auditor/owner scoped), DocumentAccessEntry model
│   └── domain-events-contract.md        # Document*/Version*/Processing* domain → integration events via outbox
└── checklists/
    └── requirements.md  # Spec quality checklist (created by /speckit.specify)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                       # untouched canon
│   └── BuildingBlocks.Kernel.Domain/..., BuildingBlocks.CQRS/..., BuildingBlocks.EventBus.RabbitMQ/...
├── Modules/
│   ├── Documents/                        # BC-05 — only module owning writes for this feature
│   │   ├── Documents.Domain/             # Aggregates: Document, DocumentVersion, DocumentProcessingJob; Entities: DocumentAccessEntry, ExplicitGrant; Enumerations: DocumentStatus, ProcessingStage, ClassificationLevel, DocumentType; VOs: Classification, ContentHash, MimeType, RetentionPolicy, MetadataSnapshot, Provenance; Rules: VersionIsImmutableRule, DocumentStatusTransitionRule, ClassificationIsValidRule, AccessAllowedRule, ProcessingStageTransitionRule; Events: DocumentUploaded, DocumentClassified, DocumentAccessed, DocumentAccessDenied, DocumentDeleted, DocumentApproved, DocumentVersionPublished, DocumentVersionSuperseded, DocumentProcessingStageCompleted, DocumentProcessingFailed; Services: IDocumentAccessPolicy, IClassificationPolicy, ISecurityScanProvider, IStorageGateway
│   │   ├── Documents.Application/        # Vertical slices: UploadDocument, PublishDocumentVersion, ClassifyDocument, ApproveDocument, RetryProcessingStage, DeleteDocument (commands) + GetDocument, ListDocumentVersions, GetAccessHistory, GetProcessingJob (queries) + pipeline handlers (ValidationHandler, VirusScanHandler, MetadataHandler, ClassificationHandler, StorageHandler, IndexingHandler) — each with Validator+Handler+IEndpoint, ISender + IPipelineBehavior
│   │   ├── Documents.Infrastructure/     # DocumentsDbContext : AppDbContextBase (HasDefaultSchema("documents"), Npgsql, RowVersion, owned MetadataSnapshot/jsonb, OutboxEntityTypeConfiguration) + EfRepository + IClassificationPolicy impl (versioned rule table + cache) + IDocumentAccessPolicy impl (IManagementHierarchy + IProjectMembership composition) + IStorageGateway impl (S3/MinIO via AWSSDK/Minio) + ISecurityScanProvider stub/adapter + Ef specifications (AuthorizedDocumentSpec, DocumentByTenantSpec, AccessHistorySpec) + ClassificationRulesSeeder
│   │   └── Documents.Contracts/          # DTOs + Integration events: DocumentUploadedIntegrationEvent, DocumentClassifiedIntegrationEvent, DocumentApprovedIntegrationEvent, DocumentDeletedIntegrationEvent, DocumentIndexedIntegrationEvent + IDocumentSearchContract (consumed by Search) + IClassificationPolicy contract
│   ├── Organization/
│   │   ├── Organization.Contracts/       # consumed — IManagementHierarchy + IAuthorizationEvaluator (permission mapping for document.read/download/approve/audit.read/processing.retry)
│   │   └── Organization.Infrastructure/  # IProjectMembership adapter is in Projects; no write to Organization
│   ├── Projects/
│   │   ├── Projects.Contracts/           # consumed — IProjectMembership.IsMember(projectId, userId) for access evaluation + Project/WorkItem existence validation
│   │   └── Projects.Infrastructure/      # IProjectMembership adapter backed by ProjectsDbContext (read-only)
│   ├── Identity/                         # not touched — permission catalog already owns document.* permission codes
│   ├── Search/                           # consumes DocumentIndexedIntegrationEvent (BC-07)
│   ├── Audit/                            # consumes all document domain→integration audit events (BC-10)
│   └── Notifications/                    # consumes DocumentApproved/ProcessingFailed
│   ├── Api/
│   │   ├── Program.cs                    # MapEndpoints picks up Documents slices via AddEndpoints(typeof(Program).Assembly) — no manual per-route registration
│   │   └── Features/                     # optional thin re-exports if Api hosts slice IEndpoints; otherwise slices live in Documents.Application
│   ├── Web/
│   │   └── src/app/features/documents/   # list/detail/history/version timeline/upload + scan status — uses documents-api-contract.md + minimal-ui-design-system + ngrx-signal-store
│   └── tests/
│       ├── Architecture/                 # existing guard — extended with Documents boundary check (no cross-module Infra refs; Documents may only ref Contracts of Organization/Projects/Identity)
│       └── Documents.Tests/              # new: Unit (VersionImmutabilityTests, ClassificationPolicyTests, AccessPolicyMatrixTests, LifecycleTests, MetadataSnapshotEqualityTests, ProcessingStageTests), Integration (PipelineIntegrationTests with MinIO+scan stub, DeduplicationTests, HashVerificationTests, RetryTests), Security (DocumentAccessSecurityMatrixTests per SPEC-013), E2E (UploadToAvailableTests)
├── OroKanban.AppHost/
│   └── AppHost.cs                        # AddMinio/AddAwsS3 resource for object storage (dev MinIO container, prod AWS S3), WithReference(postgres/rabbitmq/redis/minio) for api project + identity wiring
└── tests/
    └── Documents.Tests.Integration/      # optional split from Documents.Tests for Testcontainers suites
```

**Structure Decision**: Single bounded context `Documents` in `src/Modules/Documents` (4-layer module already scaffolded by 002) is the only source-touched module for writes; `Organization`/`Projects`/`Identity` are consumed read-only via their Shared Kernel Contracts. No new projects are scaffolded beyond slice files via `dotnet new classlib` style where needed (FR-010). All EF persistence lives in `Documents.Infrastructure` with schema `documents`; binary bytes are in S3-compatible storage, never in DB. Cross-module access tests use `IManagementHierarchy` + `IProjectMembership` thin adapters rather than direct DbContext references. Aspire AppHost adds an S3-compatible resource (MinIO for dev) alongside existing `postgres`/`redis`/`rabbitmq`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
