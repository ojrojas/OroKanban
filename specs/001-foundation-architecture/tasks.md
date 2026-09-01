# Tasks: Foundation and Architecture

**Input**: Design documents from `/specs/002-foundation-architecture/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-health-contract.md, contracts/identity-config-contract.md, quickstart.md
**Constitution**: v1.2.0 enforced — Principles I, II, III, IV, V, XXI, XXII, XV, XVI, XVIII, XIX, XX
**Discovery**: `draft/discovery/000-repository-catalog.md` ADR-001/002/003 resolved by this feature

**Tests**: Architecture guard tests are REQUIRED per FR-007 (BuildingBlocks-only, module-boundary, DbContext-inheritance). Configuration-binding unit tests and AppHost smoke/integration tests per spec TDD strategy are also required.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify tooling and prepare reproducibility log for FR-010 platform-CLI scaffolding

- [x] T001 Create reproducibility log at `docs/scaffolding-log.md` (records exact `dotnet new` / `ng new` commands + `dotnet --version` + `ng version` per FR-010)
- [x] T002 Verify .NET SDK 10.0.400 and Angular CLI availability (`dotnet --version` equals `global.json`; `npx @angular/cli@latest version` succeeds) in terminal
- [x] T003 Verify discovery gate artifact exists at `draft/discovery/000-repository-catalog.md` and contains ADR-001/002/003 entries referenced by this plan

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared package and solution scaffolding that MUST complete before ANY module or host work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Update central package pins in `Directory.Packages.props` (add `Npgsql.EntityFrameworkCore.PostgreSQL`, `Aspire.Hosting.*`, `Microsoft.Extensions.Http.Resilience`, and arch-test helper `NetArchTest.Rules` or document reflection fallback) to satisfy plan Technical Context
- [x] T005 Add solution folders for new modules in `OroKanban.slnx` (folders for `/src/Modules/Identity`, `/src/Modules/Organization`, `/src/Modules/Projects`, `/src/Modules/Metrics`, `/src/Modules/Documents`, `/src/Modules/AiProcessing`, `/src/Modules/Search`, `/src/Modules/Audit`, `/src/Modules/Notifications`, plus `/src/Api`, `/src/Web`, `/tests/Architecture`)
- [x] T006 Create `tests/Architecture/` project via `dotnet new classlib -n Architecture -o tests/Architecture -f net10.0` then convert to `xUnit` test project (add `xunit`, `Microsoft.NET.Test.Sdk`, `NetArchTest.Rules`) in `tests/Architecture/Architecture.csproj`

**Checkpoint**: Foundation ready — solution folders and central versions in place; module and host scaffolding can now begin in parallel

---

## Phase 3: User Story 1 - Platform engineer establishes solution structure and module skeletons (Priority: P1) 🎯 MVP

**Goal**: Runnable solution with 9 bounded-context modules (Domain/Application/Infrastructure/Contracts each), persistence convention, and no cross-module Infrastructure refs

**Independent Test**: `dotnet build OroKanban.slnx -warnaserror` succeeds with 0 warnings; `dotnet sln OroKanban.slnx list` shows all 9 modules + Api + Web; a grep for `Modules.*.Infrastructure` cross-references outside its own module produces no output

### Implementation for User Story 1

- [x] T007 [US1] Scaffold composition API host via `dotnet new webapi -n Api -o src/Api -f net10.0` (then delete WeatherForecast sample) in `src/Api/Api.csproj`
- [x] T008 [US1] Scaffold Angular frontend via `npx @angular/cli@latest new orokanban-web --directory src/Web --routing --style=scss --skip-git --package-manager npm` in `src/Web/` (then adapt to `minimal-ui-design-system` tokens placeholder and `ngrx-signal-store` store skeleton per plan)
- [x] T009 [P] [US1] Scaffold Identity module projects via `dotnet new classlib` (4 projects) in `src/Modules/Identity/Identity.Domain/Identity.Domain.csproj`, `src/Modules/Identity/Identity.Application/Identity.Application.csproj`, `src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj`, `src/Modules/Identity/Identity.Contracts/Identity.Contracts.csproj`
- [x] T010 [P] [US1] Scaffold Organization module projects via `dotnet new classlib` in `src/Modules/Organization/Organization.Domain/Organization.Domain.csproj`, `src/Modules/Organization/Organization.Application/Organization.Application.csproj`, `src/Modules/Organization/Organization.Infrastructure/Organization.Infrastructure.csproj`, `src/Modules/Organization/Organization.Contracts/Organization.Contracts.csproj`
- [x] T011 [P] [US1] Scaffold Projects module projects via `dotnet new classlib` in `src/Modules/Projects/Projects.Domain/Projects.Domain.csproj`, `src/Modules/Projects/Projects.Application/Projects.Application.csproj`, `src/Modules/Projects/Projects.Infrastructure/Projects.Infrastructure.csproj`, `src/Modules/Projects/Projects.Contracts/Projects.Contracts.csproj`
- [x] T012 [P] [US1] Scaffold Metrics module projects via `dotnet new classlib` in `src/Modules/Metrics/Metrics.Domain/Metrics.Domain.csproj`, `src/Modules/Metrics/Metrics.Application/Metrics.Application.csproj`, `src/Modules/Metrics/Metrics.Infrastructure/Metrics.Infrastructure.csproj`, `src/Modules/Metrics/Metrics.Contracts/Metrics.Contracts.csproj`
- [x] T013 [P] [US1] Scaffold Documents module projects via `dotnet new classlib` in `src/Modules/Documents/Documents.Domain/Documents.Domain.csproj`, `src/Modules/Documents/Documents.Application/Documents.Application.csproj`, `src/Modules/Documents/Documents.Infrastructure/Documents.Infrastructure.csproj`, `src/Modules/Documents/Documents.Contracts/Documents.Contracts.csproj`
- [x] T014 [P] [US1] Scaffold AiProcessing module projects via `dotnet new classlib` in `src/Modules/AiProcessing/AiProcessing.Domain/AiProcessing.Domain.csproj`, `src/Modules/AiProcessing/AiProcessing.Application/AiProcessing.Application.csproj`, `src/Modules/AiProcessing/AiProcessing.Infrastructure/AiProcessing.Infrastructure.csproj`, `src/Modules/AiProcessing/AiProcessing.Contracts/AiProcessing.Contracts.csproj`
- [x] T015 [P] [US1] Scaffold Search module projects via `dotnet new classlib` in `src/Modules/Search/Search.Domain/Search.Domain.csproj`, `src/Modules/Search/Search.Application/Search.Application.csproj`, `src/Modules/Search/Search.Infrastructure/Search.Infrastructure.csproj`, `src/Modules/Search/Search.Contracts/Search.Contracts.csproj`
- [x] T016 [P] [US1] Scaffold Audit module projects via `dotnet new classlib` in `src/Modules/Audit/Audit.Domain/Audit.Domain.csproj`, `src/Modules/Audit/Audit.Application/Audit.Application.csproj`, `src/Modules/Audit/Audit.Infrastructure/Audit.Infrastructure.csproj`, `src/Modules/Audit/Audit.Contracts/Audit.Contracts.csproj`
- [x] T017 [P] [US1] Scaffold Notifications module projects via `dotnet new classlib` in `src/Modules/Notifications/Notifications.Domain/Notifications.Domain.csproj`, `src/Modules/Notifications/Notifications.Application/Notifications.Application.csproj`, `src/Modules/Notifications/Notifications.Infrastructure/Notifications.Infrastructure.csproj`, `src/Modules/Notifications/Notifications.Contracts/Notifications.Contracts.csproj`
- [x] T018 [US1] Wire module project references (Domain ← Application/Infrastructure; Infrastructure → Domain + BuildingBlocks; Contracts standalone) and add all new projects to `OroKanban.slnx` via `dotnet sln add` in terminal
- [x] T019 [US1] Implement per-module DbContext inheriting `AppDbContextBase` with `OutboxEntityTypeConfiguration`, `HasDefaultSchema("<module>")`, Npgsql provider, and `RowVersion` concurrency token in `src/Modules/<Module>/<Module>.Infrastructure/Persistence/<Module>DbContext.cs` (one file per module, 9 total)
- [x] T020 [US1] Add placeholder integration event example (e.g., `OrganizationHierarchyChangedIntegrationEvent : IntegrationEvent`) in `src/Modules/Organization/Organization.Contracts/Events/OrganizationHierarchyChangedIntegrationEvent.cs` and document Contracts-only cross-module rule

**Checkpoint**: At this point, User Story 1 is fully functional — `dotnet build` 0 warnings, 9 modules with 4-layer structure, DbContexts satisfy persistence convention, no cross-module Infrastructure refs

---

## Phase 4: User Story 2 - Platform engineer composes distributed environment with external identity (Priority: P1)

**Goal**: Aspire AppHost declares Postgres/RabbitMQ/Redis + external oroidentityserver + Api/Web, and Api validates tokens via OIDC discovery with `tenant_id` propagation and fail-fast config

**Independent Test**: `aspire run` (or `dotnet run --project OroKanban.AppHost/OroKanban.AppHost.csproj`) dashboard shows Postgres, RabbitMQ, Redis, oroidentityserver (external), api, web as Healthy within 2 min; a request with a valid discovery-issued token returns 200 with `tenant_id` visible to a handler, missing/invalid token returns 401, missing Authority fails startup within 5 s with named error

### Implementation for User Story 2

- [x] T021 [US2] Declare Aspire resources in `OroKanban.AppHost/AppHost.cs` (Postgres `AddPostgres`+`AddDatabase`, RabbitMQ `AddRabbitMQ`, Redis `AddRedis`, external `oroidentityserver` via `AddContainer` + `WithEndpoint("http", 5080)`, then `AddProject<Projects.Api>` and Web via `AddNpmApp`/`AddContainer` with `WithReference`/`WaitFor` and `WithEnvironment("Identity__Authority", oroidentity.GetEndpoint("http"))`)
- [x] T022 [US2] Configure Api Program.cs JWT bearer validation against discovery endpoint in `src/Api/Program.cs` (`AddAuthentication().AddJwtBearer` with `Authority = config["Identity:Authority"]`, `Audience = config["Identity:Audience"]`, discovery metadata fetch, `tenant_id` extraction via `IClaimsTransformation`/middleware into scoped `TenantContext`)
- [x] T023 [US2] Add environment-only identity configuration bindings with fail-closed `ValidateOnStart` in `src/Api/Configuration/IdentityOptions.cs` (keys `Identity__Authority`/`Identity__Audience`/`Identity__ClientId`/`Identity__ClientSecret`; error message names missing key; never logs secrets) — referenced by `src/Api/appsettings.json` and `src/Api/appsettings.Development.json` placeholders
- [x] T024 [US2] Implement dev-only `SeedDevelopmentData` vertical slice (`ICommand<Result>` + Handler calling `POST /api/tenants`, `/api/users`, `/api/users/{id}/roles` on oroidentityserver) in `src/Api/Features/SeedDevelopmentData/SeedDevelopmentDataCommand.cs` and endpoint `src/Api/Features/SeedDevelopmentData/SeedDevelopmentDataEndpoint.cs` (guarded by `IsDevelopment()`)
- [x] T025 [US2] Implement `GetPlatformHealth` vertical slice (`IQuery<Result<PlatformHealthResponse>>` aggregating `HealthCheck` + discovery fetch `GET {Authority}/.well-known/openid-configuration`) in `src/Api/Features/GetPlatformHealth/GetPlatformHealthQuery.cs` and `src/Api/Features/GetPlatformHealth/GetPlatformHealthEndpoint.cs` per `specs/002-foundation-architecture/contracts/api-health-contract.md`

**Checkpoint**: At this point, User Stories 1 AND 2 work together — solution builds, AppHost composes all resources, tokens validate via external discovery, health and seed flows operate, fail-fast is enforced

---

## Phase 5: User Story 3 - Architect enforces quality guard with ServiceDefaults and architecture tests (Priority: P2)

**Goal**: Every service exposes OTel/Serilog health/resilience, and the Architecture suite continuously guards prohibited dependencies, module boundaries, and DbContext inheritance

**Independent Test**: Deliberately add `<PackageReference Include="MediatR" />` to any module project and run `dotnet test tests/Architecture`; the test fails within 10 s naming the offending project. Remove it and `GET /health` + `GET /alive` respond <1 s with OTel traces visible, and `GET /api/platform/health` returns modules+identity+infra sections.

### Implementation for User Story 3

- [x] T026 [US3] Wire `AddServiceDefaults()` (OTel OTLP, health checks `/health`/`/alive`, `Microsoft.Extensions.Http.Resilience`) plus `BuildingBlocks.Logger` Serilog in `src/Api/Program.cs` and ensure `OroKanban.AppHost/AppHost.cs` propagates OTLP endpoint env to services
- [x] T027 [US3] Ensure each module's `Program`/host usage (if any separate hosts) would call `AddServiceDefaults()` — document as convention in `docs/architecture/service-defaults.md` if Api is the sole host at foundation stage
- [x] T028 [US3] Implement architecture guard tests in `tests/Architecture/ArchitectureTests.cs` (test: no references to `MediatR`/`MassTransit`/`AutoMapper`; test: no `ProjectReference` from one `Modules.*` to another module's `Infrastructure` or `Domain`; test: every `DbContext` in `Modules.*.Infrastructure` inherits `AppDbContextBase` and applies `OutboxEntityTypeConfiguration`; optional: `Directory.Packages.props` contains expected pins)
- [x] T029 [US3] Add configuration-binding unit tests for fail-closed identity options in `tests/Architecture/IdentityOptionsValidationTests.cs` (Authority/Audience absent → `OptionsValidationException` with missing key name)
- [x] T030 [US3] Add AppHost smoke test in `tests/Architecture/AppHostSmokeTests.cs` (or `tests/Integration/`) that starts the AppHost in test host, asserts Postgres/RabbitMQ/Redis/external oroidentityserver/Api/Web resources are declared and `/health` is reachable (uses Aspire Testing)

**Checkpoint**: At this point, all three user stories are independently functional — foundation is observable, resilient, and guarded

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, documentation, and build-quality checks spanning all stories

- [x] T031 Run `dotnet build OroKanban.slnx -warnaserror` and fix any warnings to satisfy SC-001 in terminal
- [x] T032 Run `dotnet test tests/Architecture -v minimal` and ensure guard suite passes within 10 s, then verify the negative case (temporarily add MediatR reference, run again, see failure, revert) in terminal
- [x] T033 Run `specs/002-foundation-architecture/quickstart.md` steps 1–7 (solution structure, persistence convention, ServiceDefaults/health, identity fail-closed, architecture guard, zero-warning build, dev seed) and fix any deviation in `OroKanban.slnx` / `OroKanban.AppHost/AppHost.cs` / `src/Api/Program.cs`
- [x] T034 Append scaffolding reproducibility log with final `dotnet --version` and `ng version` output to `docs/scaffolding-log.md` per FR-010
- [x] T035 Update `README.md` with foundation run instructions (clone → `dotnet build` → `aspire run` / `dotnet run --project OroKanban.AppHost` → dashboard → `GET /health` → identity via `/.well-known/openid-configuration`) referencing `draft/discovery/000-repository-catalog.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - US1 (P1) and US2 (P1) can proceed in parallel after Foundational for planning, but US2's AppHost `AddProject` references the Api project created in US1 — so implement US1's `src/Api` scaffold (T007) first, then parallelize remaining US1 module scaffolds with US2's AppHost edits
  - US3 (P2) depends on US1 (modules and Api exist to guard) and US2 (identity wiring to include in health)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational — no other story dependencies
- **User Story 2 (P1)**: After Foundational + T007 (`src/Api` exists) — otherwise independent of US1's module persistence details; can overlap with US1 module scaffolding
- **User Story 3 (P2)**: After US1 (code to guard exists) and US2 (health/identity to observe) — may start writing tests earlier but final assertion needs prior stories' code

### Within Each User Story

- Scaffold (dotnet new / ng new) before wiring references
- Domain/Infrastructure before Application vertical slices
- Configuration bindings before AppHost that consumes them
- Tests written alongside or immediately after the code they guard, but must fail before fix and pass after

### Parallel Opportunities

- T009–T017 (9 module scaffolds) can run in parallel — different directories, no shared files (mark all [P])
- T021–T025 (AppHost + identity + health) can overlap with US1 module scaffolding once T007 is done
- T028–T030 (architecture tests) can run in parallel — different test classes, same project
- All [P] tasks across Setup and Foundational can run in parallel within their phase

---

## Parallel Example: User Story 1

```bash
# Launch all 9 module scaffolds in parallel (different directories):
Task: "Scaffold Identity module projects via dotnet new classlib in src/Modules/Identity/..."    # T009
Task: "Scaffold Organization module projects via dotnet new classlib in src/Modules/Organization/..." # T010
Task: "Scaffold Projects module projects via dotnet new classlib in src/Modules/Projects/..."    # T011
Task: "Scaffold Metrics module projects via dotnet new classlib in src/Modules/Metrics/..."     # T012
Task: "Scaffold Documents module projects via dotnet new classlib in src/Modules/Documents/..."  # T013
# ... T014–T017 similarly parallel

# Wire after scaffolds:
Task: "Wire module project references and add to slnx in OroKanban.slnx"  # T018 — after T009–T017
Task: "Implement per-module DbContext inheriting AppDbContextBase in src/Modules/<Module>/<Module>.Infrastructure/..." # T019 — after T018
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T006) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T007–T020)
4. **STOP and VALIDATE**: `dotnet build -warnaserror` 0 warnings; `dotnet sln list` shows 9 modules; grep for cross-module Infrastructure refs returns empty
5. Demo the skeletons without requiring running infrastructure

### Incremental Delivery

1. Setup + Foundational → solution ready
2. + US1 → module skeletons + persistence convention → MVP
3. + US2 → AppHost composition + external identity + health → runnable distributed app
4. + US3 → ServiceDefaults + architecture guard → observable and continuously verified
5. Each increment adds value without breaking previous

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 — Api/Web + 5 modules (Identity, Organization, Projects, Metrics, Documents)
   - Developer B: US1 — remaining 4 modules (AiProcessing, Search, Audit, Notifications) + T018 wiring, then US3's architecture tests
   - Developer C: US2 — AppHost + identity wiring + health (after T007)
3. US1 merges first (blocks US3's final assertion); US2 and US3 converge; Polish is joint validation

---

## Notes

- [P] tasks = different files/directories, no dependencies — safe to parallelize
- [Story] label maps task to user story for traceability to FR-001…FR-010 and SC-001…SC-005
- Each user story is independently testable per its Independent Test criterion
- Record every `dotnet new` / `ng new` invocation in `docs/scaffolding-log.md` — the command plus `dotnet --version` / `ng version` — per FR-010 auditability
- Avoid: manual file creation for new projects (violates FR-010), hard-coding identity Authority (violates FR-005), cross-module Infrastructure references (violates FR-002/FR-007)
