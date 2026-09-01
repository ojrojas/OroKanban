# Implementation Plan: LLM and Document Intelligence

**Branch**: `006-llm-document-intelligence` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md) | **Depends on**: 005-document-management (Document, DocumentVersion, IDocumentAccessPolicy, ChunkReference notion, outbox job machinery) + 002-identity-access-organization (TenantContext, IManagementHierarchy)

**Input**: Feature specification — BC-06 AI Processing (Core). R1 dotnet-ai technology-selection mandate (MEAI IChatClient + VectorData + DataIngestion, no provider SDK in Domain, ML.NET only for tabular), R2 provider-agnostic domain (ILLMProvider/ILLMProcessor/IDocumentExtractor/IEmbeddingProvider in Domain, Infrastructure selected by config), R3 async pipeline Document→Extraction→Normalization→Classification→Chunking→Indexing→Embedding→LLM Processing→Result→Validation→Human Review via outbox-queued jobs correlationId + retryable, R4 12 operation types (summarization..projectContextAnalysis) each with input contract/prompt version/review requirement, R5 mandatory Provenance on every LlmResult, R6 immutable LlmPromptVersion append-only, R7 Human review Generated→PendingReview→Approved|Rejected|Superseded via configurable IReviewPolicy, R8 Authorized RAG via IAuthorizedRetrievalPolicy Golden Rule B pre-filter (global indexes forbidden).

## Summary

Implement BC-06 as the bounded context that owns traceable AI intelligence over enterprise documents. `LlmOperation` + `LlmResult` + `LlmPromptVersion` + `LlmReview` persist in `ai_processing` schema (one logical PostgreSQL via Aspire `postgres`, tenant-scoped, RowVersion concurrency, append-only prompt versions, mandatory `Provenance` VO), `OperationType`/`ReviewStatus`/`OperationStatus` are `Enumeration` with `IBusinessRule` lifecycle guards, `ChunkReference`/`ModelDescriptor`/`QualityIndicator` are ValueObjects produced by deterministic `Microsoft.Extensions.AI.DataIngestion` chunker (512 tokens/50 overlap). AI stack follows `dotnet-ai` decision tree exactly: `Microsoft.Extensions.AI` `IChatClient` (abstraction) + `Azure.AI.OpenAI` or `OpenAI` provider via `AddChatClient` (Infrastructure only), `Microsoft.Extensions.VectorData.Abstractions` + connector (InMemory for dev, `Qdrant`/`PgVector` for prod via ADR-006-01) for embeddings, `Microsoft.Extensions.AI.DataIngestion` for chunking/ingestion, no ML.NET for this spec (LLM-only, no tabular task). Domain never references provider SDK per R2. Pipeline is outbox-driven reusing SPEC-005 `DocumentProcessingJob` pattern but as new `LlmOperation` aggregate with per-stage map and idempotent RabbitMQ topic `ai.processing.*` (`CorrelationId` propagates OTel), retryable `FailedRetryable→Pending` with `maxAttempts=3`. Human review is deny-by-default via `IReviewPolicy` (`operation×classification×policy` versioned table, default `requiresReview=true` safe) — `PendingReview` cannot influence business data until `Approved`, and approved proposals never silently overwrite authoritative fields (explicit `ApplyProposed*` audited commands). RAG is `AskDocumentQuestion` command that does `IEmbeddingProvider.Embed(query)` → `IAuthorizedRetrievalPolicy.FilteredSearch` (tenant + `IDocumentAccessPolicy` IsSafe/classification/subtree/project/grant as pre-filter on chunk metadata) BEFORE ranking → only authorized `ChunkReference`s reach `IChatClient`; answer enumerates `Sources` each individually `GetDocument`-authorizable; global unfiltered index is forbidden and enforced by Architecture test; prompt templates use structured `{{content}}` data boundary and `IResultValidationPolicy` flags injection (`isInjectionFlagged`). Public surface is vertical-slice `IEndpoint` + `Result→HTTP` for `QueueLlmOperation`/`RetryLlmOperation`/`PublishPromptVersion`/`RequestLlmReview`/`ApproveLlmResult`/`RejectLlmResult`/`AskDocumentQuestion` + queries `GetOperationProvenance`/`ListPendingReviews`/`GetResultHistory`.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript Angular latest (RAG UI per `minimal-ui-design-system` + `ngrx-signal-store`; contracts only for backend)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, Enumeration, ValueObject, IBusinessRule/CheckRule, Specification<T>, Result/Error, IRepository), `BuildingBlocks.CQRS` (ISender, ICommand/IQuery, ICommandHandler/IQueryHandler, IPipelineBehavior — Validation + Logging), `BuildingBlocks.EventBus` + `RabbitMQ` (IntegrationEvent, IEventBus, outbox), `BuildingBlocks.ServiceDefaults` (OTel/Serilog/health/resilience), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, OutboxEntityTypeConfiguration, UnitOfWork), `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore` (HasDefaultSchema, RowVersion), `Microsoft.Extensions.AI` (IChatClient, ChatOptions temperature 0, retry via UseChatClient), `Microsoft.Extensions.AI.Abstractions` + provider connector `Azure.AI.OpenAI` or `OpenAI` or `OllamaSharp` (concrete behind IChatClient, selected by config `AI:Provider` + `AI:ModelId`), `Microsoft.Extensions.VectorData.Abstractions` + provider connector `Qdrant.Client` or `Npgsql.Vector` (PgVector) or `InMemoryVectorStore` for dev (connector selected via ADR-006-01), `Microsoft.Extensions.AI.DataIngestion` (preview) for Ingestion/Chunking (semantic vs fixed 512/50), `Microsoft.ML.Tokenizers` (tiktoken) for client-side token counting + cost guard, `Microsoft.AspNetCore.Authentication.JwtBearer` (already in Api — provides `sub`/`tenant_id`/roles), `StackExchange.Redis` via Aspire `redis` (optional cache for review policy)

**Storage**: PostgreSQL via Aspire `postgres` — schema `ai_processing` (via `HasDefaultSchema("ai_processing")`). Tables `ai_processing.llm_operations`, `ai_processing.llm_prompt_versions`, `ai_processing.llm_results`, `ai_processing.llm_reviews`, `ai_processing.chunk_references` (or `vector_chunks` with pgvector), `ai_processing.review_policies` (versioned per tenant), `ai_processing.operation_type_catalog` (seed 12 types), `outbox_messages`. Vector embeddings in external vector store (InMemoryVectorStore in dev, Qdrant/PgVector in prod) but chunk **metadata** (`tenantId`, `documentId`, `versionId`, `classification`, `projectId`, `isSafe`, `isCurrentVersion`) lives in PG for pre-filter; embeddings themselves in vector DB keyed by `chunkId` with tenant-scoped collection/partition. Outbox per `AppDbContextBase`.

**Testing**: xUnit (`dotnet test`), NetArchTest, Testcontainers for Postgres + InMemory vector store (no external service in unit), `NSubstitute` for `IChatClient`/`IEmbeddingGenerator`/`IDocumentAccessPolicy` fakes with deterministic responses per SPEC-013, `Microsoft.AspNetCore.TestHost` for Api auth filtering. TDD: unit (Provenance completeness, ReviewPolicy matrix operation×classification×policy, Prompt immutability, ReviewStatus lifecycle, ChunkReference equality, OperationType catalog), integration (pipeline stages with mocked providers deterministic, outbox retries with `Transient` stub, vector-store connector InMemory behavior, authorized retrieval pre-filter), security (retrieval leakage: cross-branch and cross-classification MUST NOT surface protected chunks; prompt-injection regression: untrusted content as data not instruction), E2E (queue→pipeline→PendingReview→Approved→ApplyProposed, RAG with authorized sources).

**Target Platform**: Linux containers via Podman (Aspire dashboard), `oroidentityserver` external container reference already declared in `OroKanban.AppHost/AppHost.cs` (Authority via `Identity__Authority` / `Oidc__Authority`). Api is the single composition host exposing `src/Modules/AiProcessing` endpoints via vertical slices. Web `src/Web` RAG panel consumes `ask-document-question-contract.md` + `minimal-ui-design-system` + `ngrx-signal-store`.

**Project Type**: Modular monolith — this feature touches `src/Modules/AiProcessing` (new aggregates/domain services/vertical slices) and reuses `src/Modules/Documents` (Document/Version, IDocumentAccessPolicy via `Documents.Contracts`) + `src/Modules/Organization` (`IManagementHierarchy`) + `src/Api` wiring + optional `src/Web` intelligence components.

**Performance Goals**: `QueueLlmOperation` HTTP acceptance <300 ms p95 (no LLM call in-request; outbox only; SC-001); `GetOperationProvenance` <100 ms p95; `AskDocumentQuestion` end-to-end (embed→filtered retrieval top-K 5→LLM with temp 0→validation) <3 s p95 with stub provider in dev, <6 s with real provider; `ListPendingReviews` 1k entries <300 ms paginated; pipeline per-stage retry <2 s exponential backoff; 99% summarization pipeline Queued→Generated <15 s in dev (mocked providers instant).

**Constraints**: Principle I: reuse BuildingBlocks canon — no MediatR/MassTransit/AutoMapper; separate via dotnet-ai skill mandate — `Microsoft.Extensions.AI` abstractions only in Domain, provider SDKs only in Infrastructure, never mix raw HttpClient→OpenAI with MEAI in same workflow; Principle VI: rules in Domain via `CheckRule`/`IBusinessRule`/`Specification<T>` — controllers never mutate result lifecycle; Principle X: every LlmResult has mandatory Provenance VO (constructor validates, no null); Principle XI: non-authoritative until Approved, never silently overwrites (`ApplyProposed*` explicit audited); Principle XV: every `Specification<T>` includes `tenant_id`, cross-tenant returns 404 (not 403); Principle XVII: async via outbox + RabbitMQ, handlers idempotent keyed by `(OperationId, Stage)` or `EventId`, no LLM blocks HTTP after Queue; Principle XIX: deny-by-default, least privilege, no secrets in source, chunk metadata tenant-scoped; Principle VIII: append-only audit via outbox for every operation/prompt/review/RAG query; dotnet-ai skill: temperature explicit (0 for factual), retry via `UseChatClient` or `RetryingChatClient` maxRetries 3, model version pinning (`gpt-4o-2024-08-06` style), token counting via `Microsoft.ML.Tokenizers`, secret via env/KeyVault.

**Scale/Scope**: 4 aggregates (LlmOperation, LlmPromptVersion, LlmResult, LlmReview) + 6 VOs/Enumerations (OperationType, ReviewStatus, OperationStatus, Provenance, ModelDescriptor, QualityIndicator, ChunkReference) + 3 domain services (IReviewPolicy, IAuthorizedRetrievalPolicy, IResultValidationPolicy) + provider abstractions (ILLMProvider/ILLMProcessor/IDocumentExtractor/IEmbeddingProvider behind MEAI); ~6 commands + 3 queries (vertical slices) + RAG command, ~50 new files in AiProcessing module; 12 OperationTypes seeded; no new Aspire resources beyond vector store connector (reuses postgres/rabbitmq/redis).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` canon (AggregateRoot, StronglyTypedId, Enumeration, ValueObject, IBusinessRule, Specification, Result, ISender, AppDbContextBase, EfRepository, outbox, IEndpoint, Result→HTTP) and `.agents/skills/dotnet-ai/skills/technology-selection` decision tree (`IChatClient` via `Microsoft.Extensions.AI` + `VectorData` + `DataIngestion`, no provider SDK in Domain) + `ddd-project-planner` bounded context BC-06 + `minimal-ui-design-system` + `ngrx-signal-store`; no new ORM/event bus — Npgsql/RabbitMQ already via 002/005. BuildingBlocks direct reuse.
- [x] **II — oroidentityserver Mandatory**: Consumed only — `sub`/`tenant_id`/roles from JWT validated via discovery; `QueueLlmOperation`/`AskDocumentQuestion` propagate `TenantContext` as first gate. No local login.
- [x] **III — .NET 10**: All code targets `net10.0` via `global.json` + `Directory.Packages.props` central pinning.
- [x] **IV — Aspire Orchestrator**: Adds optional vector-store resource (`InMemory` in dev, `Qdrant` container in prod) alongside existing `postgres`/`redis`/`rabbitmq`; `identity-api` remains external container reference; no duplication of identity infrastructure. Vector store wired via `WithReference` pattern when external container used, else InMemory via config switch (no hard requirement on new Aspire resource for dev).
- [x] **V — Modular Architecture**: BC-06 owns `AiProcessing` module; cross-module only via `Documents.Contracts` (`IDocumentAccessPolicy`, `DocumentId`) + `Organization.Contracts` (`IManagementHierarchy`) and EventBus integration events (`LlmResultGenerated`→notifications, `DocumentAvailable`→ai). No direct DbContext cross-reference — enforced by Architecture test (Contracts + EventBus only).
- [x] **VI — Domain Rules Belong to the Domain**: `PromptIsImmutableOncePublishedRule`, `ReviewStatusTransitionRule`, `OperationStatusTransitionRule`, `ProvenanceCompleteRule`, `ChunkReferenceValidationRule` are `IBusinessRule` via `CheckRule` in Domain; `IReviewPolicy`/`IAuthorizedRetrievalPolicy`/`IResultValidationPolicy` are pure domain services, not controllers. Handlers delegate to rules.
- [x] **VII — Hierarchical Authorization**: Every `QueueLlmOperation`/`AskDocumentQuestion`/`GetResultHistory` composes tenant + `IDocumentAccessPolicy` (subtree + project membership + explicit grant + classification + IsSafe) as pre-filter via `IAuthorizedRetrievalPolicy` BEFORE vector ranking; unbounded depth via `IManagementHierarchy`; dedicated security tests per SPEC-013 matrix (cross-branch × classification).
- [x] **VIII — Everything Important Is Auditable**: Queue, prompt publish, stage completed/failed, result generated/approved/rejected/superseded, review created, RAG query executed (retrievedCount/filteredCount) all emit append-only via same-transaction outbox with `CorrelationId`; updates never mutate history.
- [x] **X — AI Must Be Traceable**: Every `LlmResult` has mandatory `Provenance` VO (SourceDocumentId/VersionId, OperationId/Type, Model, PromptVersion, CreatedAt/By, ProcessingStatus, QualityIndicator) validated at construction; no result without provenance (FR-007).
- [x] **XI — Human Approval for Sensitive AI Operations**: `ReviewStatus` `Generated→PendingReview→Approved|Rejected|Superseded` with `IReviewPolicy` (operation×classification×policy); `PendingReview` cannot influence business data; approved proposals require explicit `ApplyProposed*` audited command (FR-010, FR-022).
- [x] **XV — Tenant/Organization Aware**: Every `Specification<T>` includes `tenant_id`; `TenantContext` is first predicate; cross-tenant → 404 shadow; vector store tenant-scoped (no global index) enforced by architecture test (all vector queries through `IAuthorizedRetrievalPolicy`).
- [x] **XVI — APIs Are Contracts**: Stable request/response DTOs per slice, pagination, `CorrelationId` header, `RowVersion` concurrency via `If-Match`/body `expectedRowVersion`, `Result→HTTP` mapping (400 validation, 403 generic denial, 404 tenant-aware, 409 concurrency), OpenAPI via Aspire.
- [x] **XVII — Async Preferred**: Long operations (embedding, chunking, LLM calls) via outbox→EventBus; handlers idempotent with manual ack + exponential retries (at-least-once); no LLM blocks Queue HTTP.
- [x] **XVIII — Observability Mandatory**: `AddServiceDefaults()` OTel flow; handlers traced with `operationId`/`documentId`/`stage` baggage; `CorrelationId` correlates ai pipeline + audit; health via `/health`/`/alive` + `AiProviderHealthCheck`.
- [x] **XIX — Security by Default**: Deny-by-default, least privilege, generic deny message, prompt-injection structured boundary `{{content}}` as User data + `IResultValidationPolicy.isInjectionFlagged`, no secrets in source (AI:ApiKey via env/KeyVault), protected vector store tenant-scoped, token budget via `Microsoft.ML.Tokenizers`.
- [x] **XX — Testability Is Architectural**: Unit (provenance completeness, review policy matrix, prompt immutability, result state machine, chunk equality), integration (pipeline with mocked IChatClient/InMemory vector store, outbox retries, retrieval leakage), security (cross-branch × classification leakage 0, prompt-injection regression), E2E (queue→pipeline→review→apply).
- [x] **XXI — TDD+DDD+Vertical Slices**: Aggregates as `AggregateRoot<StronglyTypedId>`, slices as `ICommand`/`IQuery`+`Validator`+`Handler`+`IEndpoint`, manual mapping, `Result`/`Error`, `Specification<T>` for filtered queries, own `ISender`; tests precede implementation per constitution.
- [x] **XXII — Skills Govern Design**: `dotnet-ai` technology-selection (MEAI + VectorData + DataIngestion) is sole AI mandate — decision tree applied (R1 branches: LLM via IChatClient for NLP, VectorData for RAG, DataIngestion for chunking; no ML.NET for this spec since no tabular task); `ddd-project-planner` for BC-06 context map; `minimal-ui-design-system` + `ngrx-signal-store` for RAG panel. No skip-layers (MEAI foundation + provider SDK behind it + no Agent Framework since no tool-calling agentic loop required — single-prompt RAG via IChatClient is sufficient per skill layer rule).

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 expected to remain PASS (Phase 1 adds only documentation; no new gates introduced).

## Project Structure

### Documentation (this feature)

```text
specs/006-llm-document-intelligence/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── ai-operations-contract.md      # QueueLlmOperation, RetryLlmOperation, PublishPromptVersion, RequestLlmReview
│   ├── rag-query-contract.md          # AskDocumentQuestion (authorized RAG), GetOperationProvenance, GetResultHistory, ListPendingReviews
│   ├── prompt-version-contract.md     # LlmPromptVersion lifecycle, Template immutability
│   └── domain-events-contract.md      # LlmOperation*/LlmResult*/Prompt* domain → integration events via outbox
└── checklists/
    └── requirements.md  # Spec quality checklist (created by /speckit.specify)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                       # untouched canon
│   └── BuildingBlocks.Kernel.Domain/..., BuildingBlocks.CQRS/..., BuildingBlocks.EventBus.RabbitMQ/..., BuildingBlocks.EventBus/...
├── Modules/
│   ├── AiProcessing/                     # BC-06 — only module owning writes for this feature
│   │   ├── AiProcessing.Domain/          # Aggregates: LlmOperation, LlmPromptVersion, LlmResult, LlmReview; Enumerations: OperationType, OperationStatus, ReviewStatus; VOs: Provenance, ModelDescriptor, QualityIndicator, ChunkReference; Rules: PromptIsImmutableOncePublishedRule, ReviewStatusTransitionRule, ProvenanceCompleteRule; Events: LlmOperationQueued, LlmOperationCompleted, LlmOperationFailed, LlmResultGenerated, PromptVersionPublished, etc.; Services: IReviewPolicy, IAuthorizedRetrievalPolicy, IResultValidationPolicy, ILLMProvider/ILLMProcessor (IChatClient), IDocumentExtractor, IEmbeddingProvider
│   │   ├── AiProcessing.Application/     # Vertical slices: QueueLlmOperation, RetryLlmOperation, PublishPromptVersion, RequestLlmReview, ApproveLlmResult, RejectLlmResult, AskDocumentQuestion (RAG) — each with Validator+Handler+IEndpoint, ISender + IPipelineBehavior + pipeline handlers (Extraction, Normalization, Classification, Chunking, Indexing, Embedding, LlmProcessing, Validation, Review)
│   │   ├── AiProcessing.Infrastructure/  # AiProcessingDbContext : AppDbContextBase (HasDefaultSchema("ai_processing"), RowVersion, jsonb Provenance/ChunkReferences, OutboxEntityTypeConfiguration) + EfRepository + IReviewPolicy impl (versioned ai_review_policies table + default true) + IAuthorizedRetrievalPolicy impl (IDocumentAccessPolicy + IManagementHierarchy + VectorStore pre-filter) + IResultValidationPolicy impl (injection heuristic) + ChatClient adapter (OpenAI/Azure via AddChatClient, Ollama/InMemory for dev) + Embedding adapter (InMemory VectorStore for dev, Qdrant/PgVector prod) + DataIngestion chunker adapter + Ef specifications (LlmOperationByTenantSpec, PendingReviewSpec, ChunkByTenantSpec)
│   │   └── AiProcessing.Contracts/       # DTOs + Integration events: LlmOperationQueuedIntegrationEvent, LlmResultGeneratedIntegrationEvent, LlmResultApprovedIntegrationEvent, PromptVersionPublishedIntegrationEvent, RagQueryExecutedIntegrationEvent + IEmbeddingContract
│   ├── Documents/
│   │   ├── Documents.Contracts/          # consumed — DocumentId, DocumentVersionId, IDocumentAccessPolicy (IsSafe gate)
│   │   └── Documents.Infrastructure/     # chunk metadata source (document version for indexing)
│   ├── Organization/
│   │   ├── Organization.Contracts/       # consumed — IManagementHierarchy
│   │   └── Organization.Infrastructure/  # hierarchy cache (read-only)
│   ├── Search/                           # consumes LlmResultGenerated (BC-07) for re-indexing via BC-10 assumption
│   ├── Audit/                            # consumes all ai domain→integration audit events (BC-10)
│   └── Notifications/                    # consumes LlmResultGenerated/Approved
│   ├── Api/
│   │   ├── Program.cs                    # MapEndpoints picks up AiProcessing slices via AddEndpoints(typeof(Program).Assembly)
│   │   └── Features/                     # optional thin re-exports if Api hosts slice IEndpoints
│   ├── Web/
│   │   └── src/app/features/ai/          # rag panel, provenance timeline, pending review list, prompt version editor — uses rag-query-contract.md + minimal-ui-design-system + ngrx-signal-store
│   └── tests/
│       ├── Architecture/                 # existing guard — extended with AiProcessing boundary check (no provider SDK refs in Domain, all VectorStore queries via IAuthorizedRetrievalPolicy, tenant-scoped)
│       └── AiProcessing.Tests/           # new: Unit (ProvenanceCompleteness, ReviewPolicyMatrix, PromptImmutability, ReviewStatusLifecycle, ChunkReferenceEquality), Integration (PipelineWithMocks, OutboxRetry, InMemoryVectorStore, AuthorizedRetrieval), Security (CrossBranchLeakage, CrossClassification, PromptInjectionRegression), E2E (QueueToApprovedApply)
│
├── OroKanban.AppHost/
│   └── AppHost.cs                        # Optional vector-store resource (InMemory needs no container; Qdrant container for prod via AddContainer("qdrant") with volume — or PgVector via postgres extension); WithReference(postgres/rabbitmq/redis) for api + identity wiring unchanged
└── tests/
    └── AiProcessing.Tests.Integration/   # optional split for Testcontainers suites (postgres + vector store)
```

**Structure Decision**: Single bounded context `AiProcessing` in `src/Modules/AiProcessing` (4-layer module already scaffolded by 005) is the only source-touched module for writes; `Documents`/`Organization` are consumed read-only via their Shared Kernel Contracts + `IDocumentAccessPolicy` pre-filter. No new projects beyond slice files via `dotnet new classlib` style where needed (FR-022). All EF persistence lives in `AiProcessing.Infrastructure` with schema `ai_processing`; embeddings live in Vector Store (InMemoryVectorStore dev, Qdrant/PgVector prod) with tenant-scoped partition but chunk **metadata** (tenantId, classification, projectId, isSafe, isCurrentVersion) is also in PG for pre-filter validation. Cross-module tests use `IManagementHierarchy` + `IDocumentAccessPolicy` thin adapters rather than direct DbContext references. Aspire AppHost optionally adds Qdrant container for prod; dev runs InMemory with no extra container (keeps AppHost minimal).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
