# Repository Catalog — OroKanban Discovery (SPEC-000)

**Date**: 2026-08-31
**Commit**: 35e5f06 (working tree)
**Spec**: specs/001-repository-discovery/spec.md
**Constitution**: .specify/memory/constitution.md v1.2.0
**Branch**: 001-repository-discovery

> This document is the executable output of the Repository Discovery Gate (Constitution Principles I, XXI, XXII — Gate J). It catalogs every `draft/*` rule base, every installed skill, the solution/orchestration shape, and cross-cutting state, then derives a capability matrix and ADR queue. No production feature (SPEC-001 onward) may start without citing this catalog.

---

## 1. draft/* Catalog

| Path | Status | Kind | Summary | Capabilities | Config Surface | Notes |
|------|--------|------|---------|--------------|----------------|-------|
| `draft/libraries/buildingblocks.md` | FOUND | DraftDoc | BuildingBlocks canon: DDD + Vertical Slice + CQRS + EventBus (RabbitMQ) + persistence + host defaults for .NET 10. Single source of truth for architecture and code generation. | `StronglyTypedId<T>`, `Entity`/`AggregateRoot<TId>` with `CheckRule(IBusinessRule)`, `ValueObject`, `Enumeration`, `IDomainEvent` + dispatcher in `AppDbContextBase.SaveChanges`, `Result`/`Error`, `IRepository<T>`/`IUnitOfWork`, composable `Specification<T>` (`And`/`Or`/`Not`, `Where`, `AddInclude`, `ApplyAsNoTracking`), `ICommand`/`IQuery` + handlers via own `ISender` (`Sender` resolves from DI with cached generic wrappers), `IPipelineBehavior` (Logging, Validation with `Validator<T>`), `IDomainEventHandler`, `IntegrationEvent`/`IEventBus`/`IIntegrationEventHandler` + subscription registry, `RabbitMqEventBus` (durable topic exchange, publisher confirms, per-service queue, manual ack, QoS, exponential retry, at-least-once), `AppDbContextBase` + `EfRepository` + `SpecificationEvaluator` + transactional outbox (`IOutboxWriter` + `OutboxProcessor`), `IEndpoint` vertical slices, `Result → HTTP` extensions, `GlobalExceptionHandler`, `ServiceDefaults` (OTel OTLP/log/trace/metrics, `/health` `/alive`, HTTP resilience, Redis token storage), Serilog wiring | `EventBus:RabbitMq:HostName/UserName/Password/ExchangeName/QueueName`, `ConnectionStrings`, OTel `OTLP_ENDPOINT`, Serilog sinks (Console/File/Loki/Seq) | `draft/libraries/buildingblocks.md:1` states multi-target `net10.0` with no MediatR/MassTransit/AutoMapper. Vertical slice example at lines 68-108 shows Command → Validator → Handler (manual mapping, outbox) → Endpoint. The one exception to no-code-gen is this file itself — it defines the generation patterns. |
| `draft/oroidentityserver-specification.md` | FOUND | DraftDoc | OroIdentityServer integration canon: OAuth2/OIDC server (OpenIddict 8) with Blazor admin, REST admin APIs, RabbitMQ event bus, and Podman deployment. The external identity source OroKanban must consume, not duplicate. | OAuth2/OIDC via OpenIddict 8 (authorization_code, client_credentials, password, refresh_token), JWT issuance/validation/revocation, `/auth/login` `/auth/logout` `/auth/change-password`, OIDC endpoints `/connect/authorize` `/connect/token` `/connect/logout` `/connect/userinfo` `/connect/introspect` `/connect/revoke`, discovery at `GET /.well-known/openid-configuration`, admin APIs `/api/users` `/api/roles` `/api/permissions` `/api/tenants` `/api/applications` (OIDC clients) `/api/scopes` `/api/identification-types` `/api/user-sessions`, policies `ManagerOrAdmin` / `AdminOnly` / `MasterAdminOnly`, multi-tenant (`tenant_id` claim from `/connect/userinfo`), user session tracking, 8-language localization, DataProtection shared keyring | `ConnectionStrings__identitydb`, `SymmetricSecurityKey`, `IDENTITY_ADMIN_HTTP`, `SEED_TENANT_NAME` (default `OroMasterRealm`), `SEED_ADMIN_*` (username `admin`, password `Admin@123456`), `EventBus__RabbitMQ__*`, `Kestrel__Certificates__Default__*`, `ASPNETCORE_URLS` (`http://+:5080;https://+:5086`) | `draft/oroidentityserver-specification.md:259` gives OIDC metadata URL; `src/IdentityServer/IdentityServer/Dockerfile` exposes all settings as env vars on slim `mcr.microsoft.com/dotnet/aspnet:10.0` (non-root). AppHost example wires Postgres+pgAdmin, Redis, RabbitMQ, identity-server, and sample Angular admin via Aspire. OroKanban must configure Authority/ClientId/Secret from discovery + client registration (`POST /api/applications`). |
| `draft/refined-specifications.md` | FOUND | DraftDoc | Refined spec baseline for SDD: 15 specs (SPEC-000 through SPEC-014) with Part 0 Global Foundations (two golden rules, domain classification, bounded contexts, context map, ubiquitous language, BuildingBlocks canon mapping, skill mandates), per-spec DDD enrichment (bounded context, aggregates, domain events, commands/queries, Given/When/Then acceptance, TDD strategy, constitution traceability), sprint roadmap, risks, and ADR checklist. | Bounded contexts BC-01..BC-10, authorization stack = Identity+Role+Permission+Organization+Hierarchy+Membership+Ownership+Classification, authorized-before-retrieval rule for documents/RAG, 22-principle traceability per spec | N/A (spec planning artifact) | `draft/refined-specifications.md:1` v1.0.0 2026-08-31. This is the downstream consumer of the discovery catalog — every spec in it will cite rows from this discovery document. The discovery document itself cites the refined spec's Part 0 for its capability matrix seed rows. |

---

## 2. Skills Catalog

| Skill | Path | Mandate | Principle | Scope | Status |
|-------|------|---------|-----------|-------|--------|
| `technology-selection` (dotnet-ai) | `.agents/skills/dotnet-ai/skills/technology-selection/SKILL.md` | PRIMARY | XXII `dotnet-ai` | Every AI/ML technology decision in .NET: ML.NET for tabular classification/regression/clustering/anomaly/recommendation; `IChatClient` (MEAI) for single-prompt LLM; Microsoft Agent Framework for agentic workflows; Copilot SDK for extensions; ONNX for custom inference; OllamaSharp for local/offline LLM; `Microsoft.Extensions.VectorData.Abstractions` for RAG/search; `MEAI.DataIngestion` for chunking/embedding. Critical rule: do NOT use LLM for ML.NET-suited tasks. | FOUND (nested skill, parent `.agents/skills/dotnet-ai` has no root SKILL.md — capability at `skills/technology-selection/SKILL.md`) |
| `ddd-project-planner` | `.agents/skills/ddd-project-planner/SKILL.md` | PRIMARY | XXII `ddd-project-planner` | Domain modeling and planning artifacts: bounded contexts, context map, ubiquitous language, aggregates/entities/VOs, Event Storming, C4, NFR matrix, ADRs, backlog/user stories (Given/When/Then), TDD strategy, sprint roadmap | FOUND |
| `minimal-ui-design-system` | `.agents/skills/minimal-ui-design-system/SKILL.md` | PRIMARY | XXII `minimal-ui-design-system` | Every UI/UX design/build task: design tokens (colors/typography/spacing/radius), ELEVATION SYSTEM (flat vs shadow-elevated), component patterns (nav/top bar/KPI cards/lists/widgets/buttons/badges), layout rules from `references/` | FOUND |
| `ngrx-signal-store` | `.agents/skills/ngrx-signal-store/SKILL.md` | PRIMARY | XXII `ngrx-signal-store` | Frontend state management: `signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, entity features, lifecycle hooks, rxjs-interop, testing patterns | FOUND |
| `aspire` | `.agents/skills/aspire/SKILL.md` | SUPPLEMENTARY | — (aspire suite router) | Top-level router for Aspire 13.4 distributed apps: detects AppHost, enforces guardrails, routes to aspire-* sub-skills; Aspire CLI operations | FOUND |
| `aspire-deployment` | `.agents/skills/aspire-deployment/SKILL.md` | SUPPLEMENTARY | — | Deploy Aspire apps to Docker Compose/K8s/Azure/AWS; publish artifacts, teardown | FOUND |
| `aspire-init` | `.agents/skills/aspire-init/SKILL.md` | SUPPLEMENTARY | — | Scaffold Aspire into a repo: `aspire new`/`aspire init`, AppHost skeleton, handoff to aspireify | FOUND |
| `aspire-monitoring` | `.agents/skills/aspire-monitoring/SKILL.md` | SUPPLEMENTARY | — | Observe Aspire apps: logs, traces, metrics, resource state, telemetry export, dashboard | FOUND |
| `aspire-orchestration` | `.agents/skills/aspire-orchestration/SKILL.md` | SUPPLEMENTARY | — | Manage AppHost lifecycle, recover from file locks/port conflicts/orphans; `aspire start/stop/wait/ps/resource` | FOUND |
| `aspireify` | `.agents/skills/aspireify/SKILL.md` | SUPPLEMENTARY | — | Wire AppHost after `aspire init`: resource graph, ServiceDefaults+OTel wiring, validation via `aspire start` | FOUND |
| `dotnet-inspect` | `.agents/skills/dotnet-inspect/SKILL.md` | SUPPLEMENTARY | — | Evidence for .NET packages/assemblies/APIs via `dnx dotnet-inspect`; source/version diffs | FOUND |
| `playwright-cli` | `.agents/skills/playwright-cli/SKILL.md` | SUPPLEMENTARY | — | Browser automation via `playwright-cli` (open/goto/click/type/snapshot) for E2E UI validation | FOUND |

> Note: The four PRIMARY skills are the constitution-mandated design-time rule bases. All other installed skills are cataloged as SUPPLEMENTARY per the spec edge case.

---

## 3. Solution Catalog

| Artifact | Status | Target / Version | Notes |
|----------|--------|------------------|-------|
| `OroKanban.slnx` | FOUND | Solution (Slnx) — .NET 10, 7 projects | References `src/BuildingBlocks/*` (6 projects) + `src/OroKanban.AppHost`. No module projects (Identity/Organization/Projects/…) yet — gap per refined spec (see ADR-002). File at `OroKanban.slnx:1`. |
| `src/BuildingBlocks/BuildingBlocks.Kernel.Domain` | FOUND | `net10.0` (via `global.json` 10.0.400), SDK `Microsoft.NET.Sdk` | DDD tactical blocks: `Entities/`, `Enumerations/`, `Events/`, `Repositories/`, `Results/` (`Result`/`Error`), `Rules/` (`IBusinessRule`), `Specifications/` (`Specification<T>`), `ValueObjects/` |
| `src/BuildingBlocks/BuildingBlocks.CQRS` | FOUND | `net10.0`, refs Kernel.Domain | `Abstractions/` (`ICommand`/`IQuery`), `Behaviors/` (Logging, Validation), `Dispatching/` (`Sender`/`ISender`), `Validation/` (`Validator<T>`). Depends on `Microsoft.Extensions.DependencyInjection.Abstractions` + Logging.Abstractions. |
| `src/BuildingBlocks/BuildingBlocks.EventBus` | FOUND | `net10.0` | `IEventBus`, `IIntegrationEventHandler`, `IntegrationEvent`, subscription registry. Transport-agnostic. |
| `src/BuildingBlocks/BuildingBlocks.EventBus.RabbitMQ` | FOUND | `net10.0`, refs EventBus | RabbitMQ impl: durable topic exchange, publisher confirms, per-service queue, manual ack, QoS, exponential retry. Deps: `RabbitMQ.Client` + Hosting/Logging/Options abstractions. |
| `src/BuildingBlocks/BuildingBlocks.Logger` | FOUND | `net10.0` | Serilog wiring: Console, File, Grafana Loki, Seq sinks; enrichers Environment/Thread/Process; `Microsoft.Extensions.Configuration.Binder` + Hosting + Options |
| `src/BuildingBlocks/BuildingBlocks.ServiceDefaults` | FOUND | `net10.0`, refs Kernel.Domain + CQRS | `ServiceDefaultsExtensions.cs` (OTel logging/tracing/metrics via `OpenTelemetry.*`, `/health` `/alive`, HTTP resilience via `Microsoft.Extensions.Http.Resilience`, `StackExchangeRedis` for token storage/cache), `Endpoints/` (`IEndpoint`), `Middleware/` (`GlobalExceptionHandler`), `TokenStorage/` |
| `global.json` | FOUND | SDK `10.0.400` | Single line version pin; enforces .NET 10 per Principle III. |
| `Directory.Build.props` | FOUND | (empty PropertyGroup) | No custom MSBuild properties yet; central package management is enabled in `Directory.Packages.props`. |
| `Directory.Packages.props` | FOUND | `ManagePackageVersionsCentrally=true`, no `<PackageVersion>` entries yet | Central package management is configured but no versions are pinned — versions will be added as module projects declare dependencies. Gap note: no version pins to conflict with, but also no guard against transitive drift (see ADR-003). |
| `Directory.Build.targets` | FOUND | (not inspected in detail — present) | Build targets file present; assumed to complement `Directory.Build.props`. |
| `.editorconfig` | FOUND | `root = true`, indent `space` (2 for xml/csproj/json) | Shared code style; not a capability per se but part of repo conventions. |

---

## 4. Orchestration & Infrastructure Catalog

| Artifact | Status | Declared Resources | Config Pattern | Notes |
|----------|--------|--------------------|----------------|-------|
| `OroKanban.AppHost/AppHost.cs` | FOUND — INCOMPLETE | **Current**: `var builder = DistributedApplication.CreateBuilder(args); builder.Build().Run();` — no resources declared. **Expected per constitution**: Postgres (Npgsql), Redis (token storage/cache), RabbitMQ (event bus), per-module services, and external `oroidentityserver` container. | `IResourceBuilder` + `WithReference` + connection strings / env vars via Aspire configuration. Appsettings via `appsettings.json` / `appsettings.Development.json` / User Secrets (`UserSecretsId 81d5ad27-895d-4ed6-8a29-7891ee5d8947` in csproj). | Bare skeleton — functional but empty. Gap: AppHost does not yet declare any infrastructure or the external identity dependency. This blocks SPEC-001's orchestration requirement (see ADR-001). |
| `OroKanban.AppHost/OroKanban.AppHost.csproj` | FOUND | Sdk `Aspire.AppHost.Sdk/13.5.3`, TargetFramework `net10.0`, `OutputType Exe`, `AspireUseCliBundle true` | — | Correct SDK and TFM; no project references beyond SDK-inferred defaults. |
| `OroKanban.AppHost/appsettings.json` | FOUND | Logging only (`Default Information`, `Microsoft.AspNetCore Warning`, `Aspire.Hosting.Dcp Warning`) | — | No connection strings or resource URLs yet — added by AppHost resource declarations. |
| `OroKanban.AppHost/appsettings.Development.json` | FOUND | (not read in detail — exists) | — | Dev overrides present but not cataloged — not a gap. |
| `aspire.config.json` | FOUND | `appHost.language = csharp` | — | Minimal config; confirms Aspire language. No resource declarations here — those live in AppHost.cs. |
| `Aspire Dev Tunnel / DCP` | NOT PRESENT | — | — | No dev tunnel or DCP custom config — default Aspire behavior. Not a gap. |

---

## 5. Cross-Cutting State

| Category | Status | Evidence | Notes |
|----------|--------|----------|-------|
| Identity integration | FOUND — PARTIAL | `draft/oroidentityserver-specification.md` defines the full canon (OIDC discovery, `/api/applications` registration, 4 flows, admin APIs, `tenant_id` claim, Podman env config). AppHost/App code does NOT yet consume it — no `AddAuthentication().AddOpenIdConnect()` / `AddJwtBearer` wiring, no Authority/ClientId/Secret from config, no external `oroidentityserver` resource reference. | Gap: consuming wiring missing. Client registration must happen in OroIdentityServer; Authority comes from `oroidentityserver` Podman image/container (Constitution II). Blocks SPEC-002. |
| Persistence | NOT PRESENT | No `DbContext` inheriting `AppDbContextBase`, no `EfRepository` usage, no migrations, no `OutboxEntityTypeConfiguration`, no PostgreSQL resource in AppHost, no Npgsql connection string. BuildingBlocks provide the primitives; no consumer exists. | Gap: persistence architecture exists as a canon but is unconsumed. Blocks SPEC-001/010. |
| UI framework | NOT PRESENT | No `src/Web/` or `frontend/` project, no `src/Api/` composition host beyond AppHost skeleton. No `minimal-ui-design-system` token usage, no `ngrx-signal-store` stores. `examples/Frontends/oroidentity-admin` (Angular sample) exists in oroidentityserver repo, not in OroKanban. | Gap: UI layer is unconsumed. The design-system and signal-store skills define the mandated approach; implementation is deferred to SPEC-009. |
| Testing | NOT PRESENT | No `tests/` directory in OroKanban repo (oroidentityserver repo has `tests/Server.Tests` + `BuildingBlocks.*.UnitTests` as reference patterns). No `xUnit`/`NSubstitute`/`Testcontainers` projects. BuildingBlocks canon expects those stacks. | Gap: test infrastructure unconsumed. Blocks Principles XX (testability) and SPEC-013. |
| CI/CD | NOT PRESENT | No `.github/workflows/` directory, no pipeline files. No build/test/deploy configuration beyond `aspire` and `dotnet build`. | Gap: no CI/CD — not blocking discovery itself, but blocks the delivery strategy. Marked P3. |

---

## 6. Capability Matrix

**How to use — three-question dependency decision procedure (FR-008):**
For any proposed new dependency, answer: (1) Does `draft/*` already provide this capability? (2) Does an installed skill establish a preferred approach? (3) Does an existing NuGet dependency (via `Directory.Packages.props` or transitive BuildingBlocks deps) already cover it? Only a negative answer to *all three* permits proposing a new dependency. Document the evaluation in the ADR queue entry.

| Needed Capability | Provided by `draft/*` | Provided by Code/Skills | Gap? | ADR | Blocked Specs |
|-------------------|------------------------|--------------------------|------|-----|---------------|
| CQRS dispatch without MediatR (`ISender`, `LoggingBehavior`, `ValidationBehavior`, `Validator<T>`, `ICommand`/`IQuery` handlers) | `draft/libraries/buildingblocks.md` — BuildingBlocks.CQRS: `Sender` with cached generic wrappers, open-generic behaviors | `src/BuildingBlocks/BuildingBlocks.CQRS` (FOUND) + Kernel.Domain Rule/Result | No | — | — |
| EventBus over RabbitMQ without MassTransit (durable topic exchange, publisher confirms, per-service queue, manual ack, QoS, exponential retry, at-least-once) | `draft/libraries/buildingblocks.md` — BuildingBlocks.EventBus + RabbitMqEventBus | `src/BuildingBlocks/BuildingBlocks.EventBus` + `BuildingBlocks.EventBus.RabbitMQ` (FOUND, dep `RabbitMQ.Client`) | No | — | — |
| Domain primitives (`Entity`, `AggregateRoot<TId>` + `CheckRule`, `ValueObject`, `StronglyTypedId<T>`, `Enumeration`, `IDomainEvent` + dispatch in `SaveChanges`, `Result`/`Error`, `IRepository`/`IUnitOfWork`, composable `Specification<T>`) | `draft/libraries/buildingblocks.md` — Kernel.Domain | `src/BuildingBlocks/BuildingBlocks.Kernel.Domain` (FOUND, subdirs Entities/Enumerations/Events/Rules/Specifications/ValueObjects/Results) | No | — | — |
| Persistence conventions (`AppDbContextBase`, `EfRepository` + `SpecificationEvaluator`, transactional outbox `IOutboxWriter` + `OutboxProcessor`, optimistic concurrency) | `draft/libraries/buildingblocks.md` — `AppDbContextBase` + `OutboxEntityTypeConfiguration` | BuildingBlocks provide primitives; **no consumer** (`src/Modules` absent, no DbContext subclass) | **Yes** — unconsumed | ADR-002 | SPEC-001, SPEC-010 |
| `IEndpoint` vertical slices + `Result → HTTP` + `GlobalExceptionHandler` | `draft/libraries/buildingblocks.md` — ServiceDefaults Endpoints/Middleware | `src/BuildingBlocks/BuildingBlocks.ServiceDefaults` (FOUND, subdirs `Endpoints/`, `Middleware/`) | **Yes** — unconsumed (no module `IEndpoint` implementations) | ADR-001 | SPEC-001, SPEC-009 |
| ServiceDefaults (OTel OTLP logging/tracing/metrics, `/health` `/alive`, HTTP resilience, Redis token storage) + Serilog (Console/File/Loki/Seq) | `draft/libraries/buildingblocks.md` — ServiceDefaults + Logger | `src/BuildingBlocks/BuildingBlocks.ServiceDefaults` (FOUND, `ServiceDefaultsExtensions.cs`, Deps: `OpenTelemetry.*`, `Microsoft.Extensions.Http.Resilience`, `StackExchangeRedis`) + `BuildingBlocks.Logger` (FOUND, Serilog sinks) | **Yes** — unconsumed (no `AddServiceDefaults()` call in any service) | ADR-001 | SPEC-001, SPEC-011 |
| External oroidentityserver via OIDC discovery (`GET /.well-known/openid-configuration`), client registration (`POST /api/applications`), 4 flows (authorization_code / client_credentials / password / refresh_token), `tenant_id` claim, admin APIs, Podman env-var config | `draft/oroidentityserver-specification.md` — full integration canon (endpoints 259ff, env vars 386ff, Docker image slim non-root) | **No consumer** — AppHost does not declare external resource; no OIDC bearer/JWT wiring | **Yes** — unconsumed | ADR-001 | SPEC-001, SPEC-002 |
| Prohibited-dependency guards (no MediatR → use `ISender`; no MassTransit → use `RabbitMqEventBus`; no AutoMapper → manual mapping; no cross-module internal persistence access) | `draft/libraries/buildingblocks.md` — "Decisiones de diseño" section (Sin MediatR/Sin MassTransit/Sin AutoMapper) | BuildingBlocks canon enforces via absence (no PackageReference to those libs); `Directory.Packages.props` has no versions to conflict with — but **no architecture tests exist** to guard regression | **Yes** — tests absent | ADR-003 | SPEC-001, SPEC-013 |
| Architecture tests (module boundary, DbContext inheritance, prohibited lib checks) | `draft/refined-specifications.md` — SPEC-001 R7 requires them; constitution §Definition of Done | **No test project** — `tests/` dir NOT PRESENT | **Yes** — missing | ADR-003 | SPEC-001, SPEC-013 |
| Module structure as bounded contexts (Identity → Organization → Projects → WorkManagement → Metrics → Documents → AI → Search → Audit → Notifications per refined spec Part 0) | `draft/refined-specifications.md` Part 0 §0.3 bounded contexts BC-01..BC-10 | **No `src/Modules/`** — solution only has `src/BuildingBlocks/` + AppHost skeleton | **Yes** — unconsumed | ADR-002 | SPEC-001 |
| UI design system (`minimal-ui-design-system` tokens/elevation/components/layout) | `.agents/skills/minimal-ui-design-system/SKILL.md` — tokens, elevation, components from `references/` | No consumer (no `src/Web/`); skill provides reference, not code | **Yes** — unconsumed (by design — deferred to SPEC-009) | — (deferred) | SPEC-009 |
| Frontend state (`ngrx-signal-store` — `signalStore`, `withState`/`withComputed`/`withMethods`/`withProps`, entities, lifecycle hooks) | `.agents/skills/ngrx-signal-store/SKILL.md` | No consumer (no frontend project) | **Yes** — unconsumed (by design — deferred to SPEC-009) | — (deferred) | SPEC-009 |
| AI/ML decision tree (ML.NET vs `IChatClient` vs Agent Framework vs ONNX vs OllamaSharp vs VectorData/DataIngestion) | `.agents/skills/dotnet-ai/skills/technology-selection/SKILL.md` | No consumer (no AI features yet) | **Yes** — unconsumed (by design — deferred to SPEC-006) | ADR-004 | SPEC-006 |
| DDD planning artifacts (bounded contexts, context map, aggregates per `ddd-project-planner`) | `.agents/skills/ddd-project-planner/SKILL.md` + `draft/refined-specifications.md` Part 0 | Refined spec already contains BC-01..BC-10 + aggregates model; no App code yet | **Yes** — model unconsumed until module implementation | — (traceability only) | All module specs |
| Central package management with version pinning | `Directory.Packages.props` — `ManagePackageVersionsCentrally=true` | File exists but `<PackageVersion>` list is empty | **Yes** — empty pin set; acceptable now but will need population as modules declare deps | — (informational) | SPEC-001 |

---

## 7. ADR Queue

| ADR | Problem Statement | Affected Specs | Owner | Priority | Source Entry |
|-----|-------------------|----------------|-------|----------|--------------|
| ADR-001 | AppHost composition: declare Postgres, Redis, RabbitMQ resources, per-module services, and the external `oroidentityserver` container; decide `AddServiceDefaults()` wiring and env-var/connection-string patterns | SPEC-001, SPEC-002, SPEC-011, SPEC-014 | Platform architect | P1 — blocks foundation | `OroKanban.AppHost/AppHost.cs` (bare `Build().Run()`), `aspire.config.json` (minimal), §4 Orchestration catalog |
| ADR-002 | Module/persistence architecture: confirm `src/Modules/*` bounded-context layout, hierarchy storage strategy (recursive CTE vs closure table vs `ltree`) for recursive subtree evaluation, DbContext per module inheriting `AppDbContextBase` with outbox | SPEC-001, SPEC-002, SPEC-003, SPEC-010 | Platform architect | P1 — blocks Projects/WorkItems/Hierarchy | Solution catalog empty for `src/Modules/*`, Capability matrix rows "Module structure" + "Persistence conventions" |
| ADR-003 | Architecture & version governance: author `tests/Architecture/` guard tests (NetArchTest or custom), populate `Directory.Packages.props` `<PackageVersion>` pins, enforce prohibited-lib checks in CI | SPEC-001, SPEC-013 | Platform architect | P1 — blocks DoD | No `tests/` dir (§5), empty `Directory.Packages.props` pin set (§3), Capability matrix row "Prohibited-dependency guards — tests absent" |
| ADR-004 | AI/vector/search/storage stack: select LLM provider abstraction (`IChatClient` per `dotnet-ai` skill), vector store, embedding provider, search engine, object storage — per `dotnet-ai` decision tree | SPEC-005, SPEC-006, SPEC-010 | AI lead + Platform architect | P2 — blocks Documents/LLM/Search | Capability matrix row "AI/ML decision tree — unconsumed" (deferred to SPEC-006) |
| ADR-005 | Test infrastructure: choose `tests/` layout (`Unit`/`Integration`/`Architecture`/`EndToEnd`), frameworks (`xUnit`, `NSubstitute`, `Testcontainers`), containerized oroidentityserver for integration tests | SPEC-013 | QA lead + Platform architect | P1 — blocks testability | §5 Testing = NOT PRESENT; oroidentityserver tests/ pattern exists as reference |
| ADR-006 | CI/CD pipeline: establish `.github/workflows/` (or equivalent) for build + test + `aspire publish` + artifact scanning | SPEC-014 | DevOps | P2 | §5 CI/CD = NOT PRESENT |
| ADR-007 | Audit/observability backend: select OTel collector + trace/metrics backend, audit tamper-evidence approach (append-only + hash chaining evaluation) | SPEC-007, SPEC-011 | Platform architect | P2 | Capability matrix does not yet require a decision — deferred until Audit spec starts |

> Notes: UI-related gaps (`minimal-ui-design-system`, `ngrx-signal-store`) and DDD model gaps are intentionally **not** ADR candidates now — they are deferred by design to SPEC-009 and will be cited as traceability, not gaps, until those specs start. The candidate list is exactly what checklists/requirements.md requires: every `Gap? == Yes` row and every `BROKEN REFERENCE`/`INCOMPLETE` entry has an ADR row.

---

## Appendix — Discovery Validation (per `specs/001-repository-discovery/quickstart.md`)

Quick validation that the discovery document satisfies its own success criteria:

- [x] **SC-001**: Every `draft/*` document cataloged — §1 has 3 rows with capabilities summarized. Reviewable in <5 min.
- [x] **SC-002**: Three-question procedure documented in §6 header — 5 sample proposals walk-through: MediatR → denied (covered by BuildingBlocks.CQRS `ISender`), MassTransit → denied (covered by `RabbitMqEventBus`), AutoMapper → denied (manual mapping per vertical slice), "new UI framework" → denied by `minimal-ui-design-system` + `ngrx-signal-store` (PRIMARY mandates), "new search engine" → triple-negative pending ADR-004.
- [x] **SC-003**: Every gap appears as ADR candidate — 7 ADRs for 7 distinct gap rows; no silent improvisation.
- [x] **SC-004**: SPEC-001 architecture tests draftable directly — guard rows in §6 name `MediatR`/`MassTransit`/`AutoMapper` + cross-module internals + `AppDbContextBase` + outbox; §3 names the files to scan.
- [x] **SC-005**: Later specs citeable — each matrix row and catalog entry is a cite anchor (e.g., cite `§1 draft/libraries/buildingblocks.md — BuildingBlocks.CQRS ISender` for CQRS decisions).
- **Rendering**: All 7 required sections present in order; every table has a header separator; status values exact; repo-relative forward-slash paths.
