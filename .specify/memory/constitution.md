<!-- Sync Impact Report
Version change: 1.1.0 → 1.2.0
Ratified: 2026-08-31 | Last Amended: 2026-08-31
Bump rationale: MINOR — materially expanded guidance. Workspace skills under `.agents/skills/` are elevated to mandatory design-time rule bases for architecture and other resources. New Principle XXII added.
Modified principles:
  - I. Existing Repository Assets Are Authoritative: now explicitly mandates the use of the `.agents/skills/` design/architecture rule bases (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) for architecture design and related resources
  - Governance compliance review: 21 → 22 principles
  - Repository Discovery Gate: added explicit reference to `.agents/skills/` as canonical design-time skill rule bases
Added sections:
  - Principle XXII. Workspace Skills Govern Architecture & Resource Design (NON-NEGOTIABLE)
Removed sections: none
Placeholders remaining: none
Deferred TODOs: none
-->


# OroKanban Constitution

## Core Principles

### I. Existing Repository Assets Are Authoritative (NON-NEGOTIABLE)

Before implementing any new functionality, the team MUST inspect and catalog `draft/*` and all installed repository/workspace skills. The team MUST keep in mind — as the foundational rule base that supports the entire architecture and code generation — the documents under `draft/*`, specifically:

- `draft/libraries/buildingblocks.md` — the BuildingBlocks canon (DDD + Vertical Slice + CQRS + EventBus over RabbitMQ): `Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IDomainEvent`, `IBusinessRule`, `Result`/`Error`, `IRepository`, `IUnitOfWork`, composable `Specification<T>`, `ICommand`/`IQuery`/handlers, own `ISender` dispatcher, `IPipelineBehavior` (Logging, Validation), `IDomainEventHandler`, `IntegrationEvent`/`IEventBus`, `AppDbContextBase`, `EfRepository`, transactional Outbox (`IOutboxWriter` + `OutboxProcessor`), OpenTelemetry/health checks, `IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`.
- `draft/oroidentityserver-specification.md` — the canonical `oroidentityserver` integration specification (identity/authorization integration base).

Existing libraries, frameworks, utilities, abstractions, templates, and skills SHALL be reused whenever they satisfy the requirement. The team MUST NOT introduce an alternative library solely because it is more familiar or convenient. When an existing repository library conflicts with a proposed implementation, the existing repository standard wins unless an explicit Architectural Decision Record (ADR) documents the exception.

Rationale: `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md` are not mere documentation — they are the rule bases that sustain the whole architecture and all code generation (TDD + DDD + Vertical Slices). Every generated or hand-written artifact MUST conform to them.

Likewise, for architecture design and all other resources, the team MUST keep in mind and use the workspace skills under `.agents/skills/`:

- `.agents/skills/dotnet-ai` (technology-selection) — mandatory decision tree for selecting AI/ML technologies in .NET (ML.NET vs `Microsoft.Extensions.AI` vs Microsoft Agent Framework vs ONNX Runtime vs OllamaSharp vs `Microsoft.Extensions.VectorData.Abstractions`, plus RAG/embeddings via MEAI Data Ingestion). Governs every AI/LLM architectural decision.
- `.agents/skills/ddd-project-planner` — mandatory methodology for domain discovery, bounded contexts, context map, ubiquitous language, aggregates, ADRs, Event Storming, C4 model, NFR matrix, backlog/user stories, TDD strategy, and sprint roadmap.
- `.agents/skills/minimal-ui-design-system` — mandatory design system (tokens, elevation system, component patterns, layout) for every UI/UX design and build task.
- `.agents/skills/ngrx-signal-store` — mandatory state-management rule base (NgRx SignalStore: `signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, entities, lifecycle hooks, testing) for frontend state design.

### II. oroidentityserver Is Mandatory (NON-NEGOTIABLE)

Authentication and identity integration SHALL use the existing `oroidentityserver` Podman image/container. The application SHALL NOT introduce another identity server as a replacement. Aspire SHALL integrate with the external `oroidentityserver` via configuration and service discovery/network appropriate to the execution environment. The application MUST support OpenID Connect, OAuth 2.0, access tokens, refresh tokens where applicable, claims, roles, scopes, user identity, organizational relationships, and authorization policies. Identity ownership remains with `oroidentityserver`; the application owns authorization policies for business resources.

### III. .NET 10 Is the Application Platform (NON-NEGOTIABLE)

All application code SHALL target .NET 10 and use modern .NET 10 conventions and APIs. Legacy implementations SHALL NOT be introduced unless required by an existing repository dependency. `global.json` and `Directory.*.props` enforce SDK and package versions centrally.

### IV. Aspire Is the Application Orchestrator (NON-NEGOTIABLE)

The distributed application SHALL be orchestrated with .NET Aspire (currently 13.5 in repo). Aspire is responsible for local distributed development, service composition, configuration, service discovery, health integration, and developer observability. Application services SHALL NOT independently reinvent orchestration already provided by Aspire. External `oroidentityserver` is integrated as an external dependency, not duplicated by Aspire.

### V. Modular Architecture

The system SHALL be decomposed into business modules rather than exclusively around technical layers. Conceptually required modules: Identity & Access; Organization; Projects; Work Management; Planning; Metrics; Documents; AI/LLM Processing; Search/Indexing; Notifications; Audit; Monitoring/Observability. Modules SHALL expose explicit contracts. Cross-module access SHALL occur through well-defined interfaces, application contracts, or integration events (via `BuildingBlocks.EventBus`). Direct access to another module's internal persistence model is prohibited.

### VI. Domain Rules Belong to the Domain

Business rules SHALL NOT be implemented primarily in controllers, UI components, or database triggers. Rules such as who can assign a task, who can modify another manager's task, whether a task can be closed, whether a document version can be deleted, whether an LLM result requires approval, how progress is calculated, whether a subordinate may manage another subordinate, and what resources a manager can audit SHALL be represented through domain/application policies (`IBusinessRule`, `Specification<T>`, `IPipelineBehavior`, authorization policies in `BuildingBlocks.Kernel.Domain` / `BuildingBlocks.CQRS`).

### VII. Hierarchical Authorization (NON-NEGOTIABLE)

Authorization SHALL support unbounded organizational hierarchy (a manager MAY manage other managers; depth SHALL NOT be hard-coded). Permissions SHALL be evaluated on identity, role, organizational position, resource ownership, management ancestry, project membership, explicit grants, resource sensitivity, and action being performed. RBAC alone is insufficient; the system SHALL combine RBAC with hierarchical/resource-based authorization. Every query returning protected resources SHALL apply authorization filtering before returning results. Hierarchical boundaries MUST be covered by dedicated authorization tests.

### VIII. Everything Important Is Auditable (NON-NEGOTIABLE)

Security-sensitive and business-significant actions SHALL generate append-oriented, tamper-evident audit records. Minimum audited events: login-related events; authorization failures; project/task creation; assignment/status/metric changes; document upload/classification/version creation/access/deletion/approval; LLM processing and result approval/rejection; permission changes; organization hierarchy changes. Audit storage is append-only; updates SHALL create new entries, never mutate history.

### IX. Documents Are First-Class Domain Objects (NON-NEGOTIABLE)

Documents SHALL NOT be modeled merely as files attached to tasks. Each document SHALL have identity, classification, owner, security classification, version, lifecycle, metadata, content type, checksum/hash, indexing state, processing state, provenance, retention information, and access history. A published document version is immutable; corrections SHALL create a new version.

### X. AI Must Be Traceable

LLM processing is a controlled business capability. Every AI-generated result SHALL have provenance including source document, source version, processing operation, model/provider, prompt/template version, timestamp, processing status, generated result, confidence/quality indicators where available, human approval status, and responsible user/process. AI output SHALL NOT silently overwrite authoritative human-created information. The AI subsystem SHALL be provider-agnostic (`ILLMProvider`, `ILLMProcessor`, `IDocumentExtractor`, `IDocumentClassifier`, `IEmbeddingProvider` abstractions) and conform to libraries already available in `draft/*` whenever applicable.

### XI. Human Approval for Sensitive AI Operations

LLM-generated information is non-authoritative until business rules for that type explicitly permit automatic acceptance. Lifecycle SHALL be `Generated → Pending Review → Approved / Rejected`. Approval requirements SHALL be configurable by document classification, operation type, and organizational policy.

### XII. Progress Must Be Explainable

A task's percentage of completion SHALL never be an unexplained arbitrary number. Progress SHALL be calculated from configurable indicators (completed/weighted subtasks, deliverables, milestones, dates, validation criteria, manually reported progress, approved evidence, quality metrics). The system SHALL retain enough information to explain why a progress value was calculated and to trace its inputs.

### XIII. Metrics Are Configurable

Metrics SHALL be configurable rather than hard-coded into the UI. Dimensions include delivery date, lateness, content completeness, task/subtask completion, quality, criticality, risk, effort, responsibility, dependency state, document compliance, and approval state. Projects MAY define their own metric models subject to platform constraints. Metric configurations are version-aware.

### XIV. State Transitions Are Controlled

Work items SHALL use explicit state machines/transition rules (e.g., `Backlog → Planned → In Progress → Blocked → In Review → Completed`). Not every transition is valid. Transitions SHALL be authorized and auditable, enforced in the domain layer, not the UI.

### XV. Data Must Be Tenant/Organization Aware

The platform SHALL support organizational isolation. Resources SHALL have explicit ownership or organizational scope where required. A user SHALL NOT gain access to data merely because it is technically queryable. Authorization SHALL be applied before returning protected resources; search results SHALL always be authorization-filtered.

### XVI. APIs Are Contracts

Application APIs SHALL expose stable contracts defining request/response models, authorization requirements, validation, errors, pagination, filtering, sorting, and concurrency behavior. Internal domain entities SHALL NOT automatically become public API contracts. Controllers/endpoints SHALL use `IEndpoint` / `Result → HTTP` patterns from `BuildingBlocks.ServiceDefaults` and vertical-slice organization.

### XVII. Asynchronous Processing Is Preferred for Long Operations

Long-running operations (document extraction, OCR, indexing, embeddings, LLM processing, bulk imports, report generation, notifications, large conversions) SHALL NOT unnecessarily block HTTP requests. They SHALL use asynchronous/background processing via the repository's existing infrastructure (`BuildingBlocks.EventBus.RabbitMQ` with transactional outbox, background services, manual ack, exponential retries). Handlers MUST be idempotent (at-least-once delivery).

### XVIII. Observability Is Mandatory

Every production service SHALL expose logs, metrics, traces, health checks (`/health`, `/alive`), and correlation identifiers via `BuildingBlocks.ServiceDefaults` / `BuildingBlocks.Logger` (Serilog + OpenTelemetry with OTLP). Distributed operations SHALL be traceable across services. No service may ship without structured logging and health endpoints.

### XIX. Security by Default (NON-NEGOTIABLE)

The platform SHALL follow least privilege, deny-by-default, secure defaults, explicit authorization, input validation, output encoding, secret isolation, encryption in transit, protected document storage, secure token handling (Redis token storage), and auditability. Secrets SHALL NOT be committed to source control. Configuration SHALL distinguish Development/Test/Staging/Production with externally configurable identity, database, storage, search, messaging, and AI-provider settings.

### XX. Testability Is Architectural (NON-NEGOTIABLE)

Business-critical behavior SHALL be testable independently from infrastructure. Required coverage: unit tests, domain tests, application tests, integration tests, authorization tests (specifically hierarchical boundaries), API tests, persistence tests, document-processing tests, LLM workflow tests, and end-to-end tests. A feature that lacks tests for its authorization boundaries and domain rules is not done.

### XXI. TDD + DDD + Vertical Slices Is the Development Methodology (NON-NEGOTIABLE)

The platform SHALL be developed under a **TDD + DDD + Vertical Slices** architecture, and the foundational rule bases for that architecture and for all code generation are the documents in `draft/*`:

- `draft/libraries/buildingblocks.md` — BuildingBlocks canon that sustains the whole architecture: DDD building blocks (`Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `IDomainEvent`, `IBusinessRule`, `Result`/`Error`), CQRS primitives (own `ISender` dispatcher, `ICommand`/`IQuery`, `IPipelineBehavior` for Logging/Validation), Vertical Slice support (`IEndpoint`, `Result → HTTP`, `GlobalExceptionHandler`), EventBus (RabbitMQ topic exchange, publisher confirms, manual ack, exponential retries), persistence infrastructure (`AppDbContextBase`, `EfRepository`, `SpecificationEvaluator`, transactional Outbox), and host defaults (OpenTelemetry, health checks, HTTP resilience).
- `draft/oroidentityserver-specification.md` — canonical identity/authorization integration rule base.

All architecture and code generation (human or AI-assisted) MUST strictly follow these two documents. No MediatR, no MassTransit, no AutoMapper — the BuildingBlocks provide their replacements. Features are organized as vertical slices; business rules live in the domain; handlers use `Result`/`Error` and manual mapping local to each slice. Tests precede implementation (TDD) and domain tests target `AggregateRoot`/`ValueObject`/`Specification<T>` behavior independently of infrastructure.

Rationale: These two `draft/*` documents define the entire architecture and code-generation rules; deviating from them invalidates consistency, testability, and auditability of the platform.

### XXII. Workspace Skills Govern Architecture & Resource Design (NON-NEGOTIABLE)

For the design of architectures and all other resources (UI, state management, planning artifacts, AI/LLM features), the team MUST use the workspace skills located under `.agents/skills/` as mandatory rule bases:

- **`dotnet-ai` (technology-selection)** — every AI/ML technology decision in .NET MUST follow this skill's decision tree: ML.NET for structured/tabular classification, regression, clustering, anomaly detection, recommendation; `Microsoft.Extensions.AI` (`IChatClient`) for single-prompt LLM tasks; Microsoft Agent Framework for agentic workflows with tool calling; GitHub Copilot SDK for Copilot extensions; ONNX Runtime for custom model inference; OllamaSharp for local/offline LLM inference; `Microsoft.Extensions.VectorData.Abstractions` + provider connectors for semantic search/RAG; `Microsoft.Extensions.AI.DataIngestion` for document chunking/embedding ingestion. Critical rule inherited from the skill: do NOT use an LLM for tasks ML.NET handles well.
- **`ddd-project-planner`** — domain modeling, architecture recommendation, and planning artifacts (bounded contexts, context map, ubiquitous language, aggregates/entities/value objects, ADRs, Event Storming, C4 model, NFR matrix, backlog, user stories with Given/When/Then, TDD strategy, sprint roadmap) MUST be produced following this skill.
- **`minimal-ui-design-system`** — every UI design/build task MUST start from this design system: design tokens (colors, typography, spacing, radius), the ELEVATION SYSTEM (flat vs shadow-elevated surfaces), component patterns (nav, top bar, KPI cards, lists, widgets, buttons, badges), and layout rules from its `references/` files.
- **`ngrx-signal-store`** — frontend state management MUST follow NgRx SignalStore (`signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, entities, lifecycle hooks, linked state, rxjs-interop, testing patterns) per this skill.

These skills complement — and work together with — the `draft/*` rule bases (Principle XXI): `ddd-project-planner` + `buildingblocks.md` shape the DDD/Vertical Slice backend; `minimal-ui-design-system` + `ngrx-signal-store` shape the frontend; `dotnet-ai` shapes the AI/LLM subsystem. A design that contradicts an applicable skill MUST be justified via ADR, same as library conflicts (Principle I).

## Architecture & Domain Model

This section codifies the reference architecture, domain entities, and cross-cutting technical constraints that all modules MUST respect.

**Reference Architecture — Conceptual (not a mandate for 1:1 service mapping):**

```
Web / Client UI
      ↓
   API Layer
      ↓
Organization | Work Mgmt | Documents  (+ Planning/Metrics/AI/Search/Notifications)
      ↓
Application/Domain
      ↓
Persistence | Search | AI/LLM
      ↓
Audit / Events / Notifications
      ↓
oroidentityserver (Podman, external)
```

Service boundaries SHALL be justified by business, operational, or scalability requirements. DTOs, events, and persistence models are separated.

**Required Core Entities (names may adapt to repo conventions):**

- Organization: `Organization`, `User`, `Role`, `Permission`, `OrganizationUnit`, `ManagementRelationship`
- Planning/Projects: `Project`, `ProjectMember`, `ProjectMetric`, `Milestone`
- Work: `WorkItem`, `WorkItemType`, `WorkItemStatus`, `WorkItemPriority`, `WorkItemAssignment`, `WorkItemDependency`, `Subtask`, `WorkItemMetric`, `WorkItemEvidence`
- Documents: `Document`, `DocumentClassification`, `DocumentVersion`, `DocumentMetadata`, `DocumentAccess`, `DocumentProcessingJob`, `DocumentIndex`
- LLM: `LlmOperation`, `LlmPromptVersion`, `LlmResult`, `LlmReview`
- Cross-cutting: `Notification`, `AuditEntry`

**Work Item Model:** SHALL support hierarchical tasks, subtasks, dependencies, assignments, multiple participants, owner/responsible person, reviewer, priority, criticality, due date, estimated/actual effort, status, progress, tags, comments, evidence, related documents, metrics, and history.

**Document Lifecycle:** `Uploaded → Validated → Classified → Extracted → Indexed → Available → AI Processing → Human Review → Approved`. Failures are explicit, recoverable/retriable states.

**Search Architecture:** SHALL support projects, work items, documents, metadata, indexed content, tags, classifications, users, and audit information (per authorization). Results are always authorization-filtered.

**Concurrency:** Optimistic concurrency for mutable business resources where appropriate. Concurrent modifications SHALL not silently overwrite. The API SHALL return a meaningful concurrency error on stale version.

**Versioning:** Version-aware for documents, project plans, metric configurations, LLM prompts, classification rules, and important business configurations. Historical versions remain traceable.

## Development Lifecycle & Operational Standards

**Configuration:** MUST distinguish Development/Test/Staging/Production. Environment-specific values SHALL not be hard-coded. Identity, database, storage, search, messaging, and AI-provider settings are externally configurable via Aspire configuration and secrets.

**Aspire Requirements:** `OroKanban.AppHost` provides the development composition. It defines distributed resources and dependencies. External Podman-hosted `oroidentityserver` is integrated as an external dependency; the exact mechanism is determined after inspecting existing repo infrastructure. AppHost SHALL NOT duplicate identity infrastructure.

**Repository Discovery Gate (MANDATORY before any production feature):** Inspect (1) repository structure, (2) `draft/*` — with `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md` as first-class canonical rule bases for architecture and code generation (TDD + DDD + Vertical Slices), (3) existing libraries, (4) installed skills — with `.agents/skills/` (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) as mandatory design-time rule bases for architecture and resource design, (5) existing architecture, (6) Aspire configuration (`OroKanban.AppHost/AppHost.cs`), (7) identity integration, (8) persistence, (9) UI framework, (10) test infrastructure, (11) CI/CD, (12) conventions. Findings SHALL be recorded in a discovery document. No major architectural decision SHALL intentionally contradict an existing repository standard without a documented ADR exception.

**Definition of Done:** A feature is complete only when requirements are implemented, authorization is implemented and tested, validation exists, persistence is correct, auditing is implemented where required, telemetry exists where required, tests cover critical behavior (including hierarchical authorization boundaries), API contracts are documented, errors are handled, concurrency is handled, documentation is updated, repository conventions are followed, and existing libraries/skills were reused where applicable.

**Architectural Decision Records:** Significant decisions (service boundaries, persistence/search technology, object storage, messaging, LLM/embedding provider, document extraction, multi-tenancy, authorization model, deployment model) SHALL be documented as ADRs in the repository.

**Constitutional Priority (conflict resolution, highest first):** 1. Security and authorization. 2. Data integrity. 3. Auditability. 4. Existing repository constraints and libraries. 5. `oroidentityserver` integration. 6. .NET 10 compatibility. 7. Aspire orchestration. 8. Maintainability. 9. Performance. 10. Convenience. No implementation may violate a higher-priority principle to simplify development.

**Initial Delivery Strategy (incremental, system usable at each milestone):** `Foundation → Identity → Organization hierarchy → Projects → Work items → Metrics/progress → Documents → Search/indexing → LLM processing → Audit/monitoring → Notifications → Advanced analytics`.

**Final Rule — Explicitness over Magic:** A future developer SHALL be able to determine who can perform an action, why they can perform it, which organization owns the resource, where it came from, which version is current, how progress was calculated, which documents influenced an AI result, who approved it, what changed, when, and who changed it. If the system cannot answer these questions, the implementation is incomplete.

## Governance

The constitution is the supreme governance document. All practices, templates, commands, and skills defer to it.

**Amendment Procedure:** Amendments require (1) written proposal with rationale and impact on dependent templates/commands, (2) review against Repository Discovery Gate and existing ADRs, (3) approval by project maintainers, (4) migration plan if the change is breaking, and (5) version bump per policy below. Amendments are recorded in the Sync Impact Report header.

**Versioning Policy (Semantic Versioning):**

- MAJOR: Backward-incompatible governance/principle removals or redefinitions.
- MINOR: New principle/section added or materially expanded guidance.
- PATCH: Clarifications, wording, typo fixes, non-semantic refinements.

**Compliance Review:** Every pull request, design review, and delivery increment MUST verify compliance with all 22 principles, the Architecture & Domain Model, and the Development Lifecycle standards. Complexity MUST be justified. Non-compliance SHALL block merge. Use `.specify/memory/constitution.md` as the runtime check for agents and the `draft/*` + `.agents/skills/` discovery gates for implementation.

**Runtime Guidance:** Agents and skills SHALL read this constitution at runtime via the Spec Kit preset/template resolution stack. Dependent templates and commands are not modified by constitution updates but read this file for enforcement.

**Version**: 1.2.0 | **Ratified**: 2026-08-31 | **Last Amended**: 2026-08-31
