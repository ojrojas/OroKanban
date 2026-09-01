# Implementation Plan: Foundation and Architecture

**Branch**: `002-foundation-architecture` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-foundation-architecture/spec.md` — establishes the .NET 10 + Aspire foundation, 9 module skeletons as bounded contexts, persistence conventions, external `oroidentityserver` integration, ServiceDefaults, and architecture guard. Incorporates user amendment FR-010: scaffolding via platform CLIs (`ng new`, `dotnet new webapi`).

## Summary

Build the runnable technical foundation that satisfies the discovery gate ADR-001/ADR-002/ADR-003: 9 bounded-context modules (`Identity/Organization/Projects/Metrics/Documents/AiProcessing/Search/Audit/Notifications`) each with `Domain/Application/Infrastructure/Contracts`, a composition `Api` host and Angular `Web` frontend scaffolded via platform CLIs per new FR-010 (`dotnet new webapi` for .NET services, `ng new` for Angular), persistence via `AppDbContextBase`+outbox+Npgsql+row-version concurrency, AppHost declaring Postgres/RabbitMQ/Redis plus external `oroidentityserver` (OIDC discovery + `tenant_id`), all services wired through `AddServiceDefaults()` (OTel/Serilog/health/resilience), and `tests/Architecture` guarding BuildingBlocks-only dispatch, module boundaries, and DbContext inheritance.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript (Angular latest, via `ng new`), Node.js for Angular toolchain

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, Specification, Result), `BuildingBlocks.CQRS` (ISender, pipeline behaviors, IEndpoint), `BuildingBlocks.EventBus` + `RabbitMQ`, `BuildingBlocks.ServiceDefaults` (OTel OTLP, health, resilience), `BuildingBlocks.Logger` (Serilog), `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.Extensions.Http.Resilience`, `Aspire.AppHost.Sdk 13.5.3`, Angular (via `ng new`), external `oroidentityserver` Podman image (OpenIddict 8 via OIDC discovery)

**Storage**: PostgreSQL (Npgsql) via Aspire Postgres resource — one logical database or per-module database/schema as resolved in research; Redis via Aspire Redis resource (token storage/cache). Transactional outbox (`IOutboxWriter` + `OutboxProcessor`) per `AppDbContextBase`.

**Testing**: xUnit (with `dotnet test`), architecture tests via reflection/`NetArchTest` (BuildingBlocks-only + module-boundary + DbContext-inheritance checks), AppHost smoke/integration test via Aspire Testing, unit tests for configuration binding (fail-closed identity)

**Target Platform**: Linux containers via Podman, Aspire dashboard (local dev); Docker-compatible production images (non-root, slim runtime per BuildingBlocks pattern)

**Project Type**: Modular distributed web application — 9 bounded-context modules + composition API + Angular frontend, orchestrated by .NET Aspire (modular monolith that can evolve to microservices per ADR-001)

**Performance Goals**: `dotnet build OroKanban.slnx` 0 warnings, completes <2 min; `aspire run` dashboard shows all resources healthy within 2 min; architecture tests <10 s; identity fail-fast <5 s; `/health`/`/alive` <1 s

**Constraints**: Principle I: reuse BuildingBlocks canon — no MediatR/MassTransit/AutoMapper; Principle V: no cross-module Infrastructure/Domain references; Principle II/IV: oroidentityserver external only, AppHost must not duplicate identity; FR-010: scaffolding MUST use platform CLIs (`ng new` for Angular, `dotnet new webapi|classlib` for .NET, `dotnet new aspire-*` where applicable) with reproducible command+version recorded; .NET 10 + Aspire only

**Scale/Scope**: ~36 module projects (9 modules × 4 layers) + Api + Web + AppHost + Architecture tests + ServiceDefaults/BuildingBlocks (~44 projects); team-parallelizable once skeletons exist; initial skeletons minimal (empty aggregates, one DbContext per module, one sample slice if needed for smoke)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` canon and `.agents/skills` mandates; no new libraries beyond Npgsql/Angular CLI/Aspire SDK which are gaps identified in `draft/discovery/000-repository-catalog.md` (ADR-001/002). Three-question procedure satisfied.
- [x] **II — oroidentityserver Mandatory**: Consumed via OIDC discovery (`/.well-known/openid-configuration`) + client registration (`POST /api/applications`), external container reference only; no new identity server, no local password storage. Tenant via `tenant_id` from `/connect/userinfo`.
- [x] **III — .NET 10**: All new projects target `net10.0` via `global.json` 10.0.400; modern .NET 10 APIs only.
- [x] **IV — Aspire Orchestrator**: AppHost is the single composition point declaring Postgres/RabbitMQ/Redis + external oroidentityserver; services use Aspire service discovery/configuration, no ad-hoc orchestration.
- [x] **V — Modular Architecture**: 9 bounded-context modules with explicit `Contracts` (integration events/DTOs) and EventBus for cross-module communication; Architecture test enforces no cross-module Infrastructure/Domain references.
- [x] **VI — Domain Rules in Domain**: Skeleton enforces placement (rules in Domain, `IBusinessRule`/`Specification<T>`) even though rich rules arrive later — structure prevents controller-trigger anti-pattern.
- [x] **XV — Tenant/Organization Aware**: `tenant_id` claim propagated as tenant context; persistence convention prepares tenant isolation via AppDbContextBase patterns.
- [x] **XVI — APIs Are Contracts**: Vertical slices use `IEndpoint` + `Result → HTTP` with stable DTOs (Contracts), not leaking domain entities.
- [x] **XVIII — Observability Mandatory**: `AddServiceDefaults()` (OTel OTLP, health, resilience) + Serilog on every service.
- [x] **XIX — Security by Default**: Identity config fail-closed (deny when Authority/ClientId absent), secrets via env/user-secrets, configuration per environment.
- [x] **XX — Testability Architectural**: Architecture test suite + AppHost smoke test + configuration-binding unit tests satisfy the guard layer; domain tests arrive per feature spec.
- [x] **XXI — TDD+DDD+Vertical Slices via draft/***: Follows `draft/libraries/buildingblocks.md` (Sender, IEndpoint, AppDbContextBase, outbox) and `draft/oroidentityserver-specification.md` for identity — FR-010 platform-CLI scaffolding is consistent (uses the templates that encode those canons, then adapts to BuildingBlocks layering).
- [x] **XXII — Workspace Skills Govern Design**: `ddd-project-planner` bounded contexts, `dotnet-ai` not needed for this foundation (AI deferred), `minimal-ui-design-system`/`ngrx-signal-store` provide the Angular frontend's design/state mandates which the `ng new` scaffold will be adapted to.
- [x] **Gate J — Repository Discovery Gate**: Discovery document `draft/discovery/000-repository-catalog.md` exists and is the source for AppHost gap inventory (ADR-001/002/003) — this plan resolves them.
- [x] **FR-010 — Platform CLI Scaffolding**: `ng new` for `src/Web`, `dotnet new webapi` for `src/Api`, `dotnet new classlib` for module Domain/Infrastructure/Contracts (and `dotnet new` variants for AppHost wiring) — reproducible and auditable, not manual file creation.

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 is expected to remain PASS (Phase 1 only adds documentation, no new violations).

## Project Structure

### Documentation (this feature)

```text
specs/002-foundation-architecture/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── api-health-contract.md
│   └── identity-config-contract.md
└── checklists/
    └── requirements.md  # Spec quality checklist (created by /speckit.specify)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                      # existing canon — untouched
│   ├── BuildingBlocks.Kernel.Domain/
│   ├── BuildingBlocks.CQRS/
│   ├── BuildingBlocks.EventBus/
│   ├── BuildingBlocks.EventBus.RabbitMQ/
│   ├── BuildingBlocks.Logger/
│   └── BuildingBlocks.ServiceDefaults/  # also satisfies OroKanban.ServiceDefaults per discovery
├── Modules/
│   ├── Identity/            # BC-01 — authorization policies (identity external)
│   │   ├── Identity.Domain/
│   │   ├── Identity.Application/
│   │   ├── Identity.Infrastructure/  # AppDbContextBase + outbox + Npgsql
│   │   └── Identity.Contracts/
│   ├── Organization/        # BC-02
│   │   ├── Organization.Domain/ ...
│   ├── Projects/            # BC-03 (incl. WorkManagement)
│   ├── Metrics/             # BC-04
│   ├── Documents/           # BC-05
│   ├── AiProcessing/        # BC-06
│   ├── Search/              # BC-07
│   ├── Audit/               # BC-08
│   └── Notifications/       # BC-09
├── Api/                     # composition host — scaffolded via `dotnet new webapi`
│   ├── Api.csproj
│   ├── Program.cs           # AddServiceDefaults, MapEndpoints, AddCqrs
│   └── Features/
│       ├── SeedDevelopmentData/   # dev-only command via OroIdentityServer admin APIs
│       └── GetPlatformHealth/     # query: composed health + identity reachability
└── Web/                     # Angular frontend — scaffolded via `ng new orokanban-web`
    ├── angular.json
    ├── package.json
    └── src/

tests/
├── Architecture/            # guard suite — references all module projects for reflection
│   ├── ArchitectureTests.cs # MediatR/MassTransit/AutoMapper + boundary + DbContext checks
│   └── Architecture.csproj
└── (Unit/Integration/EndToEnd deferred to later specs — foundation only provides Architecture)

OroKanban.AppHost/
├── AppHost.cs               # declares Postgres, RabbitMQ, Redis, external oroidentityserver, Api, Web
├── OroKanban.AppHost.csproj # Aspire.AppHost.Sdk 13.5.3
├── appsettings.json
└── appsettings.Development.json

OroKanban.slnx                # solution folders: BuildingBlocks, Modules (×9), Api, Web, AppHost, tests
global.json                   # 10.0.400
Directory.Packages.props      # ManagePackageVersionsCentrally, now populated with Npgsql, Aspire, etc.
```

**Structure Decision**: Modular monolith with bounded-context modules (9 × 4-layer projects) + single composition `Api` host (single deployment unit that can later split per ADR-001) + Angular `Web` frontend + AppHost as the local distributed-environment composition point. The only new CLI-scaffolded apps per FR-010 are `src/Api` (`dotnet new webapi`) and `src/Web` (`ng new`); each module layer is `dotnet new classlib` (with webapi template only for `Api`). All projects target `net10.0`; all services call `AddServiceDefaults()`; cross-module communication via `Contracts` + EventBus only. The structure adapts discovery findings: BuildingBlocks stays untouched, `OroKanban.ServiceDefaults` is satisfied by `src/BuildingBlocks/BuildingBlocks.ServiceDefaults`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
