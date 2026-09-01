# Implementation Plan: Projects, Work Items and Kanban

**Branch**: `003-projects-work-kanban` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md) | **Depends on**: 002-identity-access-organization (IManagementHierarchy, IAuthorizationEvaluator, tenant context, audit outbox)

**Input**: Feature specification — BC-03 Projects & Work Management (Core). R1 Project aggregate + membership feeds Golden Rule A, R2 WorkItem aggregate typed by Enumeration taxonomy, R3 hierarchy via ParentId + ReparentWorkItem, R4 state machine with transition map + CheckRule + audit, R5 dependencies + IDependencyCycleDetector/CircularDependencyRule, R6 assignment via IAssignmentPolicy (IManagementHierarchy + project membership), R7 Kanban read-model projection (columns/swimlanes/filters/overdue, never mutates state).

## Summary

Implement BC-03 as the single bounded context that owns the project and Kanban experience. `Project` and `WorkItem`/`WorkItemDependency` aggregates persist in `projects` schema (one logical database via Aspire `postgres`, tenant-scoped), enumerated taxonomies (`WorkItemType`, `WorkItemStatus`, `WorkItemPriority`, `Criticality`, `DependencyType`, `ProjectStatus/Priority`) are seeded `Enumeration` rows, hierarchy is an adjacency list `ParentId` on `WorkItem` with recursive CTE for ancestry and cycle prevention, status transitions are a domain map guarded by `TransitionIsAllowedRule` + `IWorkItemTransitionPolicy`, dependencies are cycle-checked via `IDependencyCycleDetector` (graph DFS in domain), assignment is hierarchical via `IAssignmentPolicy` composing `IManagementHierarchy` + project membership, concurrency is optimistic `Version` (row-version), and the Kanban board is an EF read model composed with authorization `Specification<T>` before fetch — every write flows through a vertical-slice command with validation behavior and transactional outbox.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript Angular latest (board UI consumes contracts; design system per `minimal-ui-design-system` skill)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, Enumeration, IBusinessRule/CheckRule, ValueObject, Specification<T>, Result/Error, IRepository), `BuildingBlocks.CQRS` (ISender, ICommand/IQuery, ICommandHandler/IQueryHandler, IPipelineBehavior — Validation + Logging), `BuildingBlocks.EventBus` + `RabbitMQ` (IntegrationEvent, IEventBus, outbox), `BuildingBlocks.ServiceDefaults` (already wired — OTel/Serilog/health/resilience), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, OutboxEntityTypeConfiguration, UnitOfWork), `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore` (row-version concurrency), `Microsoft.AspNetCore.Authentication.JwtBearer` (already in Api — provides `sub`/`tenant_id`/roles), `StackExchange.Redis` via Aspire `redis` (no extra cache needed for board — EF query; hierarchy cache already in Organization)

**Storage**: PostgreSQL via Aspire `postgres` — schema `projects` (via `HasDefaultSchema("projects")`). Tables `projects.work_items`, `projects.projects`, `projects.project_members`, `projects.project_milestones`, `projects.work_item_dependencies`, `projects.work_item_tags` (owned collection or join), `projects.enumerations` (or per-enumeration tables) for taxonomy seeds, plus `outbox_messages`. Redis via Aspire `redis` is reused only for `IManagementHierarchy` (already owned by Organization); board requires no additional Redis — it is an EF read model. Outbox per `AppDbContextBase`.

**Testing**: xUnit (`dotnet test`), NetArchTest, Testcontainers for Postgres (and Redis for hierarchy probes), `NSubstitute` for `IManagementHierarchy` fakes in assignment tests, `Microsoft.AspNetCore.TestHost` for Api auth filtering. TDD: unit (transition map exhaustive, cycle detector, assignment policy, reparenting rules, VO validation, concurrency), integration (hierarchy CTE persistence, board query with filters + authorization composition, outbox events, row-version conflict), E2E (Kanban drag/drop → ChangeWorkItemStatus → board re-query). Security matrix reuses the 8 actor types from 002 plus project-membership cross-branch case.

**Target Platform**: Linux containers via Podman (Aspire dashboard), `oroidentityserver` external container reference already declared in `OroKanban.AppHost/AppHost.cs` (Authority via `Identity__Authority` / `Oidc__Authority`). Api is the single composition host exposing `src/Modules/Projects` endpoints via vertical slices.

**Project Type**: Modular monolith — this feature touches `src/Modules/Projects` (new aggregates/domain services/vertical slices) and `src/Modules/Organization` (consume-only via `IManagementHierarchy`/`IAuthorizationEvaluator` contracts) plus `src/Api` wiring (endpoint mapping, TenantContext propagation already from 002) and `src/Web` (Kanban board component using contracts; no board mutation).

**Performance Goals**: `ChangeWorkItemStatus` validation + CheckRule <100 ms; `AddDependency` cycle detection <200 ms on 100-node graph; `GetKanbanBoard(projectId)` with 50 items <500 ms p95; Kanban drag/drop E2E (query→command→re-query) <1 s; transition-map unit suite 100% pair coverage; two-concurrent-update race resolves with one 409 in <1 s (SC-004).

**Constraints**: Principle I: reuse BuildingBlocks canon — no MediatR/MassTransit/AutoMapper; Principle VI: rules in Domain via `CheckRule`/`IBusinessRule`/`Specification<T>` — UI never sets status; Principle VII: unbounded hierarchy depth, every query composes subtree/project-membership `Specification<T>` before fetch; Principle VIII: append-only audit via outbox for every business write and deny; Principle XIV: transitions authorized + auditable + enumeration-backed map, never UI-driven; Principle XVI: stable API contracts (IEndpoint + Result→HTTP) with pagination/filtering/sorting/concurrency; Principle XII/XIII: `ProgressValue`/`Effort` as ValueObjects, not arbitrary numbers; Principle XXI: TDD+DDD+Vertical Slices with own `ISender`; FR-010: any new project/file via platform CLIs (`dotnet new classlib` for new slice folders) not manual copy; tenant_id is first gate.

**Scale/Scope**: 2 aggregates + 1 dependency aggregate, 7 Enumeration VOs, 4 ValueObjects, 3 domain services, ~9 commands + 4 queries (vertical slices), 1 read-model projection, ~40 new files in Projects module; seed taxonomy 4 WorkItemTypes + 6 statuses + priorities/criticalities; no new Aspire resources (reuses postgres/redis/rabbitmq).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` canon (AggregateRoot, Enumeration, Specification, Result, ISender, AppDbContextBase, EfRepository, outbox, IEndpoint) and `.agents/skills/ddd-project-planner` + `minimal-ui-design-system` + `ngrx-signal-store` mandates; no new libraries — Npgsql/Redis already in 002. No MediatR/MassTransit/AutoMapper.
- [x] **II — oroidentityserver Mandatory**: Consumed only — `sub`/`tenant_id`/roles from JWT validated via discovery; no local login, no password storage. Tenant propagated via existing `TenantContext` from 002.
- [x] **III — .NET 10**: All code targets `net10.0`.
- [x] **IV — Aspire Orchestrator**: No new AppHost resources; reuses `postgres`/`redis`/`rabbitmq` already declared; `identity-api` remains external container reference.
- [x] **V — Modular Architecture**: BC-03 owns `Projects` module; cross-module only via `Organization.Contracts` (`IManagementHierarchy`, `IAuthorizationEvaluator`) and EventBus integration events (`WorkItemAssigned`→notification). No direct DbContext cross-reference — enforced by Architecture test.
- [x] **VI — Domain Rules Belong to the Domain**: `TransitionIsAllowedRule`, `CircularDependencyRule`, `ReparentNoCycleRule`, `AssignmentPolicy`, `Effort/ProgressValue` invariants, `Tag` normalization are `IBusinessRule`/`CheckRule`/`ValueObject` in Domain; controllers/components never encode them.
- [x] **VII — Hierarchical Authorization**: Assignment + visibility compose `IManagementHierarchy.IsInSubtree` and project-membership `Specification<T>` before fetch; unbounded depth via same-aggregate ParentId; all board/task queries authorization-filtered.
- [x] **VIII — Everything Important Is Auditable**: Creation, status transition, assignment, reparenting, dependency changes, authorization denials all emit append-only audit via same-transaction outbox (Audit BC consumes outbox topic).
- [x] **XII — Progress Must Be Explainable**: `ProgressValue` is a validated VO (0–100); aggregation from subtasks is via explicit `ProgressRecalculated` domain event, not an arbitrary number. Full metrics wiring deferred to SPEC-004.
- [x] **XIII — Metrics Configurable**: Project metrics wiring is an interface placeholder consumed via `IProjectMetrics` — no hard-coded metric UI.
- [x] **XIV — State Transitions Are Controlled**: `WorkItemStatus` Enumeration + `IWorkItemTransitionPolicy` map with `TransitionIsAllowedRule`; UI never sets `status` — only `ChangeWorkItemStatus` command; all transitions audited and authorized via SPEC-002 evaluator.
- [x] **XV — Tenant/Organization Aware**: Every `Specification<T>` includes `tenant_id`; `TenantContext` is first gate in `IAssignmentPolicy`/`IAuthorizationEvaluator` delegation.
- [x] **XVI — APIs Are Contracts**: Stable request/response DTOs per slice, pagination/filtering/sorting envelopes, `Version` concurrency via `If-Match`/body `expectedVersion`, `Result→HTTP` mapping (400 validation, 403 generic denial, 409 concurrency via `Error.Conflict`).
- [x] **XVII — Async Preferred**: Long operations (if any) via outbox→EventBus; notification integration events are outbox-published, handlers idempotent. Board is polling/query-based — no forced WebSocket.
- [x] **XVIII — Observability Mandatory**: `AddServiceDefaults()` OTel flow; handlers traced, audit correlated via `correlationId`.
- [x] **XIX — Security by Default**: Deny by default, least privilege, no deny-reason leak, `Version` prevents silent overwrite, input validation via `Validator<T>` behavior.
- [x] **XX — Testability Is Architectural**: Unit (transition exhaustive, cycle, assignment matrix, reparenting, VO validation, Version), integration (hierarchy CTE, board query with filters + auth composition, outbox, row-version 409), E2E (drag/drop → command → projection), authorization matrix (cross-branch without membership → denied+audited).
- [x] **XXI — TDD+DDD+Vertical Slices**: Aggregates as `AggregateRoot<StronglyTypedId>`, slices as `ICommand`/`IQuery`+`Validator`+`Handler`+`IEndpoint`, manual mapping, `Result`/`Error`.
- [x] **XXII — Skills Govern Design**: `ddd-project-planner` bounded context BC-03, `minimal-ui-design-system` tokens/elevation for board UI, `ngrx-signal-store` for board SignalStore (no contradiction — backend is pure DDD).

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 expected to remain PASS (Phase 1 adds only documentation; no new gates introduced).

## Project Structure

### Documentation (this feature)

```text
specs/003-projects-work-kanban/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── projects-api-contract.md       # CreateProject, AddProjectMember, project queries
│   ├── workitems-api-contract.md      # Create/Reparent/ChangeStatus/Assign/Dependency/Complete work items + Version concurrency
│   ├── kanban-board-contract.md       # GetKanbanBoard read model — columns/swimlanes/filters/sorting/pagination/overdue, never mutates
│   └── domain-events-contract.md      # Project*/WorkItem*/Dependency* domain → integration events via outbox
└── checklists/
    └── requirements.md  # Spec quality checklist (created by /speckit.specify)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                       # untouched canon
│   └── BuildingBlocks.Kernel.Domain/..., BuildingBlocks.CQRS/..., BuildingBlocks.EventBus.RabbitMQ/...
├── Modules/
│   ├── Projects/                         # BC-03 — only module touched by this feature
│   │   ├── Projects.Domain/              # Aggregates: Project, WorkItem, WorkItemDependency; Enumerations: WorkItemStatus/Type/Priority/Criticality/DependencyType/ProjectStatus; VOs: Effort/ProgressValue/DueDate/Tag; Rules: TransitionIsAllowedRule, CircularDependencyRule, ReparentNoCycleRule, WorkItemNotCompletedRule; Events: ProjectCreated..DependencyRemoved; Services: IDependencyCycleDetector, IAssignmentPolicy, IWorkItemTransitionPolicy
│   │   ├── Projects.Application/         # Vertical slices: CreateProject, AddProjectMember, CreateWorkItem, ReparentWorkItem, ChangeWorkItemStatus, AssignWorkItem, AddDependency, RemoveDependency, CompleteWorkItem (commands) + GetKanbanBoard, GetWorkItemDetail, GetMyTasks, GetTeamTasks (queries) — each with Validator+Handler+IEndpoint
│   │   ├── Projects.Infrastructure/      # ProjectsDbContext : AppDbContextBase (HasDefaultSchema("projects"), Npgsql, RowVersion«Version», owned Tag collection, OutboxEntityTypeConfiguration) + EfRepository + IDependencyCycleDetector/InMemory + IAssignmentPolicy impl + IWorkItemTransitionPolicy impl + Ef specifications (AuthorizedWorkItemSpec, ProjectMemberSpec)
│   │   └── Projects.Contracts/           # DTOs + Integration events: WorkItemCreatedIntegrationEvent, WorkItemStatusChangedIntegrationEvent, WorkItemAssignedIntegrationEvent, etc. + IProjectMembership contract (consumed by Organization)
│   ├── Organization/
│   │   ├── Organization.Contracts/       # consumed only — IManagementHierarchy + IAuthorizationEvaluator contracts (already from 002)
│   │   └── Organization.Infrastructure/  # IProjectMembership adapter backed by ProjectsDbContext (thin read) — narrow, read-only cross-module query
│   ├── Identity/                         # not touched — only evaluator already owns permission mapping
│   └── (other modules untouched: Metrics, Documents, AiProcessing, Search, Audit, Notifications)
├── Api/
│   ├── Program.cs                        # MapEndpoints picks up Projects slices via AddEndpoints(typeof(Program).Assembly) — no manual per-route registration
│   └── Features/                         # optional thin re-exports if Api hosts slice IEndpoints; otherwise slices live in Projects.Application
├── Web/
│   └── src/app/features/kanban/          # board component: columns by status, swimlanes by assignee/epic, filters, progress/criticality, overdue — uses kanban-board-contract.md + minimal-ui-design-system + ngrx-signal-store
└── tests/
    ├── Architecture/                     # existing guard — extended with Projects boundary check (no cross-module Infra refs)
    └── Projects.Tests/                   # new: Unit (TransitionMapTests, CircularDetectorTests, AssignmentPolicyTests, ReparentTests, VoTests, VersionTests), Integration (HierarchyPersistence, BoardQuery, Outbox, ConcurrencyConflictTests), E2E (KanbanDragDropTests)
```

**Structure Decision**: Single bounded context `Projects` in `src/Modules/Projects` (4-layer module already scaffolded by 002) is the only source-touched module; `Organization` is consumed read-only via its Shared Kernel contracts. No new Aspire resources or projects are scaffolded beyond slice files via `dotnet new classlib` style where needed (FR-010). All EF persistence lives in `Projects.Infrastructure` with schema `projects`; board is an EF read model, not a separate search index. Cross-module tests use `IProjectMembership` thin adapter rather than direct DbContext references.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
