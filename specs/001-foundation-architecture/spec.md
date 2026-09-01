# Feature Specification: Foundation and Architecture

**Feature Branch**: `002-foundation-architecture`

**Created**: 2026-08-31

**Status**: Draft

**Input**: User description: "SPEC-001 — Foundation and Architecture. Bounded Context: BC-10 Platform (Generic). Depends on: SPEC-000. Objective: Establish the .NET 10 + Aspire technical foundation: solution structure, module skeletons, persistence conventions, and the external identity integration — all conforming to BuildingBlocks canon. Requirements R1 Solution structure with Modules/Identity/Organization/Projects/Metrics/Documents/AiProcessing/Search/Audit/Notifications + Api/Web + tests, R2 Module skeleton DDD layering, R3 Persistence convention AppDbContextBase + outbox + Npgsql + optimistic concurrency, R4 Aspire AppHost composition, R5 External identity integration via OIDC discovery, R6 ServiceDefaults OTel/health/resilience/Serilog, R7 Architecture tests."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Platform engineer establishes solution structure and module skeletons (Priority: P1)

As a platform engineer starting the OroKanban build, I want a well-structured solution with module skeletons that follow the bounded-context decomposition, so that later domain teams can work in parallel without reinventing infrastructure.

**Why this priority**: This is the constitutional foundation (Principle V Modular Architecture, Principle XXI TDD+DDD+Vertical Slices, Principle I reuse of BuildingBlocks). Without it, every later module would diverge and violate the discovery gate (draft/discovery/000-repository-catalog.md). Blocks all other specs.

**Independent Test**: Can be fully tested by cloning the repo, running `dotnet build OroKanban.slnx`, and verifying the solution opens with `src/BuildingBlocks/` untouched plus `src/Modules/<9 modules>` each with `Domain/Application/Infrastructure/Contracts` and `src/Api/` + `src/Web/` present, and that a reference from one module to another module's Infrastructure fails code review.

**Acceptance Scenarios**:

1. **Given** a clean checkout, **When** I open `OroKanban.slnx`, **Then** it lists `BuildingBlocks` (6 existing projects), `OroKanban.AppHost`, `OroKanban.ServiceDefaults`, `src/Modules/Identity`, `Organization`, `Projects`, `Metrics`, `Documents`, `AiProcessing`, `Search`, `Audit`, `Notifications`, `src/Api/`, `src/Web/` (Angular), and `tests/` projects without broken references.
2. **Given** any module, **When** I inspect its folders, **Then** it contains `Domain/` (aggregates/VOs/specifications/events/rules), `Application/` (vertical slices), `Infrastructure/` (DbContext + outbox), and `Contracts/` (integration events/DTOs) and no module project references another module's Infrastructure or Domain project.
3. **Given** a module persists an aggregate, **When** I inspect its DbContext, **Then** it inherits `AppDbContextBase`, applies `OutboxEntityTypeConfiguration`, uses Npgsql, and the aggregate has a row-version concurrency token.

---

### User Story 2 - Platform engineer composes the distributed environment with external identity (Priority: P1)

As a platform engineer, I want the Aspire AppHost to compose all infrastructure, modules, and the external OroIdentityServer so that `aspire start` brings up the entire platform with token validation working and no duplicate identity logic.

**Why this priority**: Satisfies Principle IV (Aspire orchestrator) and Principle II (oroidentityserver mandatory, external). Without a runnable composition, integration testing and later domain work cannot start. Same P1 as US1 — together they deliver the runnable foundation.

**Independent Test**: Can be tested by running `aspire run` (or `dotnet run --project src/OroKanban.AppHost`) and verifying the Aspire dashboard lists Postgres, RabbitMQ, Redis, the Api host, the Web frontend, and an external `oroidentityserver` resource, and that the Api's health endpoint validates a token issued by the external server.

**Acceptance Scenarios**:

1. **Given** AppHost configuration, **When** the AppHost starts, **Then** Postgres, RabbitMQ, and Redis are declared as resources, the Api/Web services are registered with references to them, and the external `oroidentityserver` is referenced as an external container/service (not re-implemented) and appears in the dashboard.
2. **Given** a valid token from `GET /.well-known/openid-configuration` on the external server, **When** the Api receives it on a protected endpoint, **Then** the token is validated (issuer/audience) and the `tenant_id` claim from `/connect/userinfo` is available to handlers.
3. **Given** identity settings (Authority, ClientId, ClientSecret) are missing in any environment, **When** a service starts, **Then** it fails fast with a clear error stating which setting is absent — it does not silently fall back to defaults or allow unauthenticated access.

---

### User Story 3 - Architect enforces quality guard with ServiceDefaults and architecture tests (Priority: P2)

As an architect, I want ServiceDefaults and architecture tests to guard the foundation so that observability, resilience, and prohibited-dependency rules are continuously enforced.

**Why this priority**: Satisfies Principles XVIII (observability), XVII (async), XIX (security by default), and the constitutional Definition of Done. Provides the continuous guard that every later spec relies on, but is only useful after US1/US2 create the code to guard.

**Independent Test**: Can be tested by deliberately adding a `PackageReference` to `MediatR` (or a cross-module Infrastructure reference) and running `dotnet test tests/Architecture/` — the test must fail with an explicit message naming the prohibited dependency.

**Acceptance Scenarios**:

1. **Given** any service, **When** it starts, **Then** `/health` and `/alive` respond, logs are structured via Serilog (BuildingBlocks.Logger), and OpenTelemetry traces/metrics are emitted via OTLP endpoint from `AddServiceDefaults()`.
2. **Given** the Architecture test suite, **When** it runs, **Then** it reports zero MediatR/MassTransit/AutoMapper references, zero cross-module Infrastructure/Domain references, and every module DbContext inherits `AppDbContextBase` with the outbox configuration applied.
3. **Given** `SeedDevelopmentData` (dev-only), **When** it runs against the external OroIdentityServer admin APIs, **Then** it bootstraps an organization, users, and role assignments without storing credentials locally; and `GetPlatformHealth` returns composed health (modules + external identity reachability).

---

### Edge Cases

- What happens when `oroidentityserver` Podman image is not running locally? The AppHost must declare it as an external resource with a configurable endpoint — `aspire start` should still bring up other resources and the Api should report identity-unreachable in `GetPlatformHealth`, not crash the whole host.
- What happens when two modules define aggregates with the same table name? Each module owns its DbContext/schema or schema-prefix — cross-module `DbSet` collision must be prevented by convention and architecture tests.
- What happens when a developer adds a direct `ProjectReference` from `Modules.Projects` to `Modules.Documents.Infrastructure` to "share a query"? The architecture test must fail and CI must block the PR with a message citing Constitution Principle V (module contracts only via interfaces/events).
- What happens when a migration targets the wrong provider (e.g., SqlServer instead of Npgsql)? The module's DbContext registration enforces Npgsql; a mismatched provider is caught by the `Infrastructure` layer's design-time factory test.
- What happens when an environment provides partial identity config (e.g., Authority but no Audience/ClientId)? Binding validation fails closed with a structured `Error` / `ProblemDetails` that lists exactly which fields are missing.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a solution structure under `src/` with `BuildingBlocks/` (existing canon untouched), `OroKanban.AppHost/` (Aspire orchestration), `OroKanban.ServiceDefaults` (if not already at `src/BuildingBlocks/ServiceDefaults`), `src/Modules/` containing nine modules `Identity`, `Organization`, `Projects`, `Metrics`, `Documents`, `AiProcessing`, `Search`, `Audit`, `Notifications` (each aligned to BC-01..BC-09), plus `src/Api/` (composition host) and `src/Web/` (Angular latest) per `OroKanban.slnx` — adapting minor naming only per discovery findings.
- **FR-002**: Each module MUST follow DDD layering: `Domain/` (aggregates, value objects, specifications `Specification<T>`, domain events `IDomainEvent`, business rules `IBusinessRule`), `Application/` (vertical slices: `ICommand`/`IQuery` + handlers + `Validator<T>` + `IEndpoint`), `Infrastructure/` (EF Core `DbContext` inheriting `AppDbContextBase` with outbox and `EfRepository`, plus `IRepository` implementation), `Contracts/` (integration events `IntegrationEvent` and public DTOs). No module project may reference another module's `Infrastructure` or `Domain` project (only `Contracts` or `Application` contracts).
- **FR-003**: Every module DbContext MUST inherit `BuildingBlocks.Kernel.Domain`'s `AppDbContextBase`, apply `OutboxEntityTypeConfiguration` in `OnModelCreating`, use Npgsql (PostgreSQL) via Aspire resource, and configure optimistic concurrency via a row-version/concurrency token on mutable aggregates so concurrent saves produce a stale-version error.
- **FR-004**: `OroKanban.AppHost` MUST compose the distributed environment by declaring Aspire resources for PostgreSQL (with Npgsql), RabbitMQ (with RabbitMQ.Client via `BuildingBlocks.EventBus.RabbitMQ`), Redis (token storage/cache via `BuildingBlocks.ServiceDefaults`), plus per-module services and `src/Api`/`src/Web`; and it MUST declare the external `oroidentityserver` as an external container/service resource with endpoint configuration — it MUST NOT re-implement or duplicate any identity logic.
- **FR-005**: The system MUST consume `oroidentityserver` as the authoritative identity source: discover metadata at `GET /.well-known/openid-configuration`, support client registration via `POST /api/applications` (or admin UI) with `authorization_code` + `refresh_token` grants, and configure Authority/ClientId/ClientSecret (and audience/scope) exclusively via environment-specific configuration; multi-tenancy MUST map to the `tenant_id` claim published by `/connect/userinfo` and must be propagated as tenant context to downstream handlers.
- **FR-006**: All services (Api, any module host, Web BFF if applicable) MUST invoke `AddServiceDefaults()` (from `BuildingBlocks.ServiceDefaults`) to wire OpenTelemetry OTLP (logs/traces/metrics), health checks at `/health` and `/alive`, and resilient HTTP (`Microsoft.Extensions.Http.Resilience`), and MUST use Serilog via `BuildingBlocks.Logger` for structured logging.
- **FR-007**: The test suite MUST include an `tests/Architecture/` project that enforces at minimum: (a) BuildingBlocks-only dispatch — zero references to MediatR, MassTransit, AutoMapper; (b) module boundary — no reference from one module to another module's `Infrastructure` or `Domain` assembly; (c) persistence convention — every module DbContext inherits `AppDbContextBase` and applies `OutboxEntityTypeConfiguration`. The suite runs in CI and fails the build on violation.
- **FR-008**: The platform MUST provide a dev-only `SeedDevelopmentData` command that bootstraps an organization, users, and role assignments via the external OroIdentityServer admin APIs (no local credential storage), and a `GetPlatformHealth` query that returns composed health of modules plus external identity reachability (reachable/unreachable with reason).
- **FR-009**: The build MUST compile on .NET 10 (SDK from `global.json`) with zero warnings for analyzer-enabled projects; the Aspire composition must be discoverable in the dashboard when started; and identity configuration absence must cause fail-fast with a clear error naming the missing setting rather than silent fallback.
- **FR-010**: Project scaffolding for new applications MUST use the platform-defined CLI commands — e.g., Angular applications via `ng new` (Angular CLI latest), .NET services/APIs via `dotnet new webapi` (or `dotnet new web` / `dotnet new classlib` for library modules as appropriate), and Aspire-related projects via `dotnet new aspire-*` where applicable — rather than manual file creation, copy-paste, or generic templates. The chosen command and its version/options MUST be recorded in the implementation log so the scaffolding is reproducible.

### Key Entities

- **Module** (bounded context): Represents one business capability (Identity, Organization, Projects, Metrics, Documents, AiProcessing, Search, Audit, Notifications). Attributes: name, solution folder, project set (Domain/Application/Infrastructure/Contracts), owning bounded context, dependencies from discovery catalog.
- **Module DbContext**: Per-module EF Core context inheriting `AppDbContextBase`. Attributes: provider (Npgsql), connection string reference (Aspire resource), applied configurations (Outbox, concurrency token), schema ownership.
- **Integration Event**: Cross-module contract inheriting `IntegrationEvent`. Attributes: event type, payload DTO, producing module, consuming module(s), outbox persistence, RabbitMQ topic routing.
- **External Identity Resource**: The `oroidentityserver` Podman container consumed via OIDC discovery. Attributes: discovery endpoint URL, registered client (ClientId/ClientSecret/redirectUri/grants), Authority/Audience mapping, `tenant_id` claim propagation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A new developer can clone the repo and run `dotnet build OroKanban.slnx` with zero warnings on a machine with only .NET 10 SDK installed — build completes in under 2 minutes on a standard dev machine.
- **SC-002**: `aspire run` (or `dotnet run --project src/OroKanban.AppHost`) starts within 2 minutes and the Aspire dashboard shows Postgres, RabbitMQ, Redis, Api, Web, and the external `oroidentityserver` resource as distinct, healthy entries.
- **SC-003**: A request bearing a valid token from the external server's discovery endpoint is accepted and its `tenant_id` claim is available to business handlers; a request with a missing/invalid token is rejected with 401 — both verifiable without inspecting implementation code.
- **SC-004**: The Architecture test suite detects a prohibited `MediatR` package reference within 10 seconds of running and reports which project introduced it, blocking the build.
- **SC-005**: Starting any service with identity settings absent (empty Authority/Audience) fails in under 5 seconds with an error that names the missing setting, rather than allowing unauthenticated operation.

## Assumptions

- New application/project creation follows platform CLIs per FR-010: `ng new` requires Angular CLI (latest), `dotnet new` requires .NET SDK 10.0.400 (from `global.json`); both are assumed available on the dev machine or installed as part of setup.
- Repository root already contains the BuildingBlocks canon (`src/BuildingBlocks/`, `Directory.Packages.props` with `ManagePackageVersionsCentrally`, `global.json` SDK 10.0.400) and the bare `OroKanban.AppHost` — this spec extends that skeleton, it does not replace it.
- `oroidentityserver` runs as a Podman container per `draft/oroidentityserver-specification.md` and is already reachable when discovery states external — the Api is a relying party, not a host.
- Module skeletons are initially minimal (empty aggregates, one DbContext per module, one sample vertical slice if needed for smoke testing) — rich domain logic arrives in later specs.
- `src/Web` will be Angular (latest) per the requirement — scaffolding may be via `ng new` or equivalent, but architecture tests still apply to its backend-for-frontend if any.
- Discovery gate SPEC-000 has already produced `draft/discovery/000-repository-catalog.md` and its ADR queue (ADR-001 composition, ADR-002 modules/persistence, ADR-003 tests/versions) — this spec resolves those ADRs concretely.
- Tests in this spec are the guard layer only; domain behavior tests arrive with each feature spec. The Definition of Done's authorization/audit/telemetry items are satisfied at the composition/observability level here, not at per-feature domain depth.
