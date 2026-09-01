# Implementation Plan: Metrics, Progress and Planning

**Branch**: `004-metrics-progress-planning` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md) | **Depends on**: 003-projects-work-kanban (WorkItem/Milestone placeholder), 002-identity-access-organization (IManagementHierarchy, IAuthorizationEvaluator)

**Input**: Feature specification — BC-04 Metrics & Progress (Core). R1 versioned MetricDefinition per project/template (dimension/weight/target/threshold/evidence), R2 pluggable IProgressCalculationStrategy Σ(w×p)/Σw with manual override audited, R3 deterministic ProgressExplanation reconstructible, R4 DeadlineStatus VO OnTime|AtRisk|Overdue|CompletedLate|CompletedOnTime, R5 versioned Milestone dated/verifiable/linked to work items, R6 subtree-filtered manager dashboards via IManagementHierarchy.

## Summary

Implement BC-04 as the metrics and planning authority that makes progress explainable. `MetricDefinition` (versioned, dimension Enumeration) and `Milestone` (versioned, linked work items) persist in `metrics` schema (one logical Postgres via Aspire `postgres`, tenant-scoped). `IProgressCalculationStrategy` is a per-project pluggable strategy (`WeightedSubtaskStrategy` default, `DeliverableMilestoneStrategy`) computing `Σ(progress×weight)/Σweight` over weighted components; every computation persists a `ProgressExplanation` (strategy, weightsSum, components, inputsSnapshot, zeroWeight handling, isOverride) that is append-only and historically queryable. `IDeadlineEvaluator` derives `DeadlineStatus` pure UTC from `DueDate+WorkItemStatus+atRiskWindowDays`. `IMetricEvaluationPolicy` emits `MetricThresholdViolated`. Read models `GetProjectHealth`/`GetManagerDashboard` aggregate via `IManagementHierarchy` subtree/membership **before** fetch. Triggers are on-demand + EventBus `WorkItemStatusChanged/Completed` from SPEC-003 (idempotent). Every metric/override/milestone write flows through a vertical-slice command with Validation behavior and transactional outbox.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript Angular latest (dashboard UI consumes contracts; design system per `minimal-ui-design-system`, state per `ngrx-signal-store` with `withRequestStatus`/`withLogger`)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, Enumeration, IBusinessRule/CheckRule, ValueObject, Specification<T>, Result/Error, IRepository), `BuildingBlocks.CQRS` (ISender, ICommand/IQuery, ICommandHandler/IQueryHandler, IPipelineBehavior Validation+Logging), `BuildingBlocks.EventBus` + `RabbitMQ` (IntegrationEvent, IEventBus, outbox), `BuildingBlocks.ServiceDefaults` (OTel/Serilog/health/resilience), `BuildingBlocks.Kernel.Infrastructure` (AppDbContextBase, EfRepository, SpecificationEvaluator, OutboxEntityTypeConfiguration, UnitOfWork), `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore` (RowVersion), `Microsoft.AspNetCore.Authentication.JwtBearer` (provides `sub`/`tenant_id`), `StackExchange.Redis` via Aspire `redis` (only for IManagementHierarchy cache already in Organization)

**Storage**: PostgreSQL via Aspire `postgres` — schema `metrics` (`MetricsDbContext : AppDbContextBase`, `HasDefaultSchema("metrics")`). Tables `metrics.metric_definitions` (versioned rows with `code+projectId+version` unique, `RowVersion`), `metrics.metric_definitions_history` or version rows, `metrics.milestones` (versioned), `metrics.milestone_work_items` (join), `metrics.progress_explanations` (append per computation, `workItemId+computedAt` index), `metrics.metric_values` (threshold evaluation). `outbox_messages` via `AppDbContextBase`. Reads of WorkItem subtasks for strategy use `ProjectsDbContext` read-only via `IWorkItemSnapshotProvider` (thin adapter, no cross-schema FK). Redis reused only for hierarchy.

**Testing**: xUnit (`dotnet test`), NetArchTest, Testcontainers for Postgres, `NSubstitute` for `IManagementHierarchy`/`IAuthorizationEvaluator` fakes, `Microsoft.AspNetCore.TestHost` for auth. TDD battles: unit (strategy arithmetic determinism, weight normalization zeroWeight, deadline transitions across UTC midnight, explanation completeness, version append), integration (metric version history, milestone version, explanation append+historical `asOf`, dashboard subtree-filtered aggregation, threshold violation → both health+dashboard visible), E2E (SPEC-003 event → auto recalculation → dashboard reflects). Security matrix: subtree vs cross-branch manager.

**Target Platform**: Linux containers via Podman (Aspire dashboard), `oroidentityserver` external container already declared in `OroKanban.AppHost/AppHost.cs`. Api is single composition host exposing `src/Modules/Metrics` endpoints via vertical slices.

**Project Type**: Modular monolith — this feature touches `src/Modules/Metrics` (new aggregates/VOs/services/slices) plus `src/Modules/Projects` read-only (WorkItem snapshot for strategy) and `src/Modules/Organization` consume-only (`IManagementHierarchy`/`IAuthorizationEvaluator`) plus `src/Web` (dashboard/health widgets).

**Performance Goals**: Progress recalc with 20 components <100ms p95; `ExplainProgress` determinism byte-identical <50ms; `GetProjectHealth` <200ms; `GetManagerDashboard` with 100 tasks across 5 subordinates <500ms p95; deadline evaluation <10ms pure; zeroWeight path never division-by-zero.

**Constraints**: Principle I: reuse BuildingBlocks canon — no MediatR/MassTransit/AutoMapper; VI: `WeightValidRule`/`ThresholdRule`/`DeadlineRule` via `CheckRule`/`IBusinessRule` in Domain; VII: dashboards filtered via subtree/membership `Specification<T>` before fetch, never post-filtered; VIII: metric/override/milestone writes + `audit.progress.overridden`/`MetricThresholdViolated` via same-tx outbox append-only; XII: `ProgressExplanation` persisted, never arbitrary number; XIII: `MetricDimension` Enumeration seedable, not hard-coded; XV: tenant-aware; XVI: stable `Result` contracts; XXI: vertical slices `ICommand/IQuery+Validator+Handler+IEndpoint`.

**Scale/Scope**: 3 aggregates (`MetricDefinition`, `MetricValue`, `Milestone` + `ProgressExplanation` append entity), `MetricDimension` Enumeration (10 dims), 5 VOs (`MetricWeight/Target/Threshold/DeadlineStatus/ComponentValue`), 3 domain services (`IProgressCalculationStrategy` + 2 impls, `IMetricEvaluationPolicy`, `IDeadlineEvaluator`), ~5 commands + 3 queries (slices), 2 read-model aggregations, ~45 new files in Metrics module.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` canon + `.agents/skills/ddd-project-planner` bounded context BC-04 + `.agents/skills/minimal-ui-design-system`/`ngrx-signal-store` (dashboard store with `withRequestStatus`/`withLogger`); no new libs — Npgsql already in 003.
- [x] **II — oroidentityserver Mandatory**: Consumed only — `progress.override`/`metric.define` via `IAuthorizationEvaluator`; no local login.
- [x] **III — .NET 10**: All code `net10.0`.
- [x] **IV — Aspire Orchestrator**: No new AppHost resources; reuses `postgres`/`rabbitmq`/`redis`; `identity-api` external.
- [x] **V — Modular Architecture**: BC-04 owns `Metrics` module; cross-module only via `Organization.Contracts` (`IManagementHierarchy`, `IAuthorizationEvaluator`) + `Projects.Contracts` read of `WorkItem` snapshot + EventBus `workitem.*` → progress recalc. No direct DbContext cross-reference — architecture test enforces.
- [x] **VI — Domain Rules Belong to the Domain**: `IProgressCalculationStrategy` arithmetic, `WeightValidRule`, `ThresholdValidRule`, `DeadlineDerivationRule`, `MilestoneCriteriaRule` are `IBusinessRule`/`CheckRule` in Domain.
- [x] **VII — Hierarchical Authorization**: `GetManagerDashboard`/`GetProjectHealth` compose `IManagementHierarchy.GetSubtree` + `IProjectMembership` `Specification<T>` before aggregation; `OverrideProgressManually` gated `progress.override`.
- [x] **VIII — Everything Important Is Auditable**: `DefineMetric`, `UpdateMetricDefinition`, `OverrideProgressManually` (actor+previous+justification → `audit.progress.overridden`), `MilestoneReached/Slipped`, `MetricThresholdViolated` all via same-tx outbox append-only.
- [x] **XII — Progress Must Be Explainable**: Every `Σ(w×p)/Σw` persists `ProgressExplanation` (strategy, weightsSum, components with contribution, inputsSnapshot, zeroWeight) deterministically reconstructible via `ExplainProgress(asOf)`.
- [x] **XIII — Metrics Are Configurable**: `MetricDefinition` per project/template, `MetricDimension` Enumeration seedable, `MetricWeight/Target/Threshold` VOs; plans/milestones version-aware (append, never overwrite).
- [x] **XV — Tenant/Organization Aware**: Every metric/milestone/explanation row carries `TenantId`; dashboards tenant-filtered first.
- [x] **XVI — APIs Are Contracts**: Stable DTOs `Result<MetricDefinitionResponse>`/`ProgressExplanationResponse`/`DashboardResponse` with `Result→HTTP` 400/403/409, never leaking entities.
- [x] **XVII — Async Preferred**: SPEC-003 event-triggered recalc via RabbitMQ topic `workitem.*` (idempotent handlers, at-least-once).
- [x] **XVIII — Observability Mandatory**: `AddServiceDefaults()` traces, `ExplainProgress` includes `computedAt` for audit trail.
- [x] **XIX — Security by Default**: deny-by-default `progress.override`, input validation via `Validator<T>`, no deny-reason leak.
- [x] **XX — Testability Is Architectural**: Unit (strategy determinism, zeroWeight, deadline midnight, explanation completeness), integration (version history, historical asOf, dashboard subtree, threshold→both read models), E2E (event→recalc→dashboard).
- [x] **XXI — TDD+DDD+Vertical Slices**: Aggregates `AggregateRoot<StronglyTypedId>`, slices `ICommand/IQuery+Validator+Handler+IEndpoint`, manual mapping, `Result/Error`.
- [x] **XXII — Skills Govern Design**: `ddd-project-planner` BC-04, `minimal-ui-design-system` tokens for health cards, `ngrx-signal-store` `withRequestStatus`/`withLogger` + `withSelectedEntity` for dashboard.

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 expected to remain PASS.

## Project Structure

### Documentation (this feature)

```text
specs/004-metrics-progress-planning/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── metrics-api-contract.md        # DefineMetric, UpdateMetricDefinition per project/template
│   ├── planning-api-contract.md       # CreateMilestone, EvaluateMilestone, versioned plan
│   ├── progress-api-contract.md       # OverrideProgressManually, Recalculate, ExplainProgress (history)
│   └── dashboards-contract.md         # GetProjectHealth, GetManagerDashboard (subtree-filtered)
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/                     # untouched canon
├── Modules/
│   ├── Metrics/                        # BC-04 — only module touched (new)
│   │   ├── Metrics.Domain/             # Aggregates: MetricDefinition, MetricValue, Milestone; VOs: MetricDimension(Enumeration), MetricWeight/Target/Threshold, DeadlineStatus, ProgressExplanation/ComponentValue; Rules: WeightValid, Threshold, DeadlineDerivation, MilestoneCriteria; Services: IProgressCalculationStrategy (WeightedSubtask, DeliverableMilestone), IMetricEvaluationPolicy, IDeadlineEvaluator; Events: MetricDefinitionCreated/Updated, MetricThresholdViolated, MilestoneReached/Slipped, ProgressOverridden
│   │   ├── Metrics.Application/        # Slices: DefineMetric, UpdateMetricDefinition, OverrideProgressManually, CreateMilestone, EvaluateMilestone (commands) + GetProjectHealth, GetManagerDashboard, ExplainProgress, RecalculateProgress (queries) — each with Validator+Handler+IEndpoint; Subscribers: WorkItemStatusChangedHandler → Recalculate
│   │   ├── Metrics.Infrastructure/     # MetricsDbContext : AppDbContextBase (HasDefaultSchema("metrics"), Npgsql RowVersion, append tables), EfRepository, Strategy impls, DeadlineEvaluator pure, MetricEvaluationPolicy, IWorkItemSnapshotProvider (reads ProjectsDbContext), IHierarchy shim
│   │   └── Metrics.Contracts/          # DTOs (MetricDefinitionResponse, ProgressExplanationResponse, DashboardResponse, DeadlineStatusDto) + Integration events (MetricThresholdViolatedIntegrationEvent, MilestoneReachedIntegrationEvent) + IProgressExplanationReader
│   ├── Projects/                       # BC-03 — read-only: WorkItem snapshot for strategy + ProgressExplanation placeholder consumed via Metrics.Contracts
│   ├── Organization/                   # consumed only — IManagementHierarchy + IAuthorizationEvaluator (already from 002)
│   └── (other modules untouched: Identity, Documents, Notifications, Audit, Search, AiProcessing)
├── Api/
│   └── Program.cs                      # MapEndpoints picks up Metrics slices via AddEndpoints(...)
└── Web/
    └── src/app/features/dashboard/     # dashboard store (signalStore withRequestStatus+withLogger, withSelectedEntity for selection) + health cards (minimal-ui-design-system elevated cards)
└── tests/
    ├── Architecture/                   # existing guard — extended with Metrics boundary check
    └── Metrics.Tests/                  # new: Unit (WeightedStrategyTests, ZeroWeightTests, DeterminismTests, DeadlineBoundaryTests, ExplanationTests), Integration (MetricVersionHistory, MilestoneVersion, ExplainAsOf, DashboardSubtree, ThresholdViolation→both models), E2E (EventTrigger→Recalc→Dashboard)
```

**Structure Decision**: Single bounded context `Metrics` in `src/Modules/Metrics` (4-layer module already scaffolded by 001) is the only source-touched module; `Projects` is read-only via `IWorkItemSnapshotProvider` thin adapter (no direct DbContext reference); `Organization` consumed via Shared Kernel contracts. No new Aspire resources; all EF persistence in `MetricsDbContext` with schema `metrics`; dashboards are EF read models with authorization `Specification<T>` before aggregation.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
