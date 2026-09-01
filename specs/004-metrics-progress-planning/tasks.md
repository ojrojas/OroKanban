# Tasks: Metrics, Progress and Planning

**Input**: Design documents from `/specs/004-metrics-progress-planning/` (spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md) | **Branch**: `004-metrics-progress-planning` | **Date**: 2026-09-01

**Tests**: Constitution XX/XXI mandates coverage — every strategy/weight/deadline/determinism path must be unit-tested, version history/dashboard subtree and event-trigger are integration-tested, and authorization boundaries (progress.override) are tested. Tests are therefore REQUIRED and MUST be written FIRST and FAIL before implementation (TDD).

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify Metrics module scaffolding and test harness — no domain code yet.

- [X] T001 Verify Metrics module scaffolding per plan.md in `src/Modules/Metrics/Metrics.Domain/`, `Metrics.Application/`, `Metrics.Infrastructure/`, `Metrics.Contracts/` (4 classlibs `net10.0`) and `src/Api/Api.csproj` composition host
- [X] T002 Create/verify xUnit test project `tests/Metrics.Tests/Metrics.Tests.csproj` (net10.0, refs: Metrics.Domain, Metrics.Application, Metrics.Infrastructure, Metrics.Contracts, BuildingBlocks.Kernel.Domain, xUnit, NSubstitute, FluentAssertions, Testcontainers.PostgreSql) and add to `OroKanban.slnx` via `dotnet sln add`
- [X] T003 [P] Add/verify package refs centralised in `Directory.Packages.props` (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore`) and run `dotnet build OroKanban.slnx -warnaserror` — 0 warnings
- [X] T004 [P] Verify AppHost `OroKanban.AppHost/AppHost.cs` reuses `postgres`/`rabbitmq`/`redis` (no new resource) and `oroidentityserver` external — `aspire run` Healthy

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared kernel, StronglyTypedIds, Enumerations, VOs, MetricsDbContext, service contracts and snapshot adapter — MUST complete before ANY user story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 Create StronglyTypedIds `MetricDefinitionId`, `MetricValueId`, `MilestoneId` in `src/Modules/Metrics/Metrics.Domain/Ids/MetricsIds.cs` (`: StronglyTypedId<Guid>`)
- [X] T006 [P] Create Enumerations `MetricDimension`, `DeadlineStatus` in `src/Modules/Metrics/Metrics.Domain/Enumerations/` (`: Enumeration` — 10 dimensions + 5 deadline statuses per data-model.md)
- [X] T007 [P] Create ValueObjects `MetricWeight` (0–1), `MetricTarget` (0–100), `MetricThreshold` (0–100), `ComponentValue` in `src/Modules/Metrics/Metrics.Domain/ValueObjects/` (validated at construction)
- [X] T008 [P] Create `MetricsDbContext : AppDbContextBase` in `src/Modules/Metrics/Metrics.Infrastructure/Persistence/MetricsDbContext.cs` (`HasDefaultSchema("metrics")`, `OutboxEntityTypeConfiguration`, `IX_progress_explanations_workItem_computedAt`, RowVersion on current rows)
- [ ] T009 [P] Create EF configurations `MetricDefinitionConfiguration`, `MilestoneConfiguration`, `ProgressExplanationConfiguration`, `MetricValueConfiguration` in `src/Modules/Metrics/Metrics.Infrastructure/Persistence/Configurations/` (append tables, `code+projectId+version` unique, `milestone_work_items` join jsonb for components/snapshot)
- [ ] T010 Create domain service contracts `IProgressCalculationStrategy` (+ `IStrategyResolver`), `IMetricEvaluationPolicy`, `IDeadlineEvaluator`, `IWorkItemSnapshotProvider` in `src/Modules/Metrics/Metrics.Domain/Services/` (pure, tenant-aware)
- [ ] T011 Create `IWorkItemSnapshotProvider` → `ProjectsDbContext` read-only adapter stub in `src/Modules/Metrics/Metrics.Infrastructure/Services/WorkItemSnapshotProvider.cs` (returns `SubtaskSnapshot[]` for `WeightedSubtaskStrategy`)
- [ ] T012 [P] Register Metrics module DI `AddMetricsModule(IServiceCollection)` in `src/Modules/Metrics/Metrics.Infrastructure/DependencyInjection.cs` (DbContext Npgsql, strategies factory, `IDeadlineEvaluator`, `IMetricEvaluationPolicy`, `IWorkItemSnapshotProvider`) and wire in `src/Api/Program.cs` via `AddMetricsModule()` + `AddCqrs` tracking
- [ ] T013 Create EF migration `Metrics_004_Initial` via `dotnet ef migrations add Metrics_004_Initial --project src/Modules/Metrics/Metrics.Infrastructure --startup-project src/Api/Api.csproj --context MetricsDbContext` and verify `dotnet ef database update` creates schema `metrics`

**Checkpoint**: Foundation ready — `dotnet build` 0 warnings, `metrics` schema exists, `MetricDimension` seeded. User stories can now begin.

## Phase 3: User Story 1 - Project defines and versions configurable metrics (Priority: P1) 🎯 MVP

**Goal**: Manager defines per-project/template metrics (dimension/weight/target/threshold/evidence) and updates create new version (append, history queryable, duplicate/invalid → Validation).

**Independent Test**: `DefineMetric` (delivery-date, DeadlineAdherence, weight 0.3, target 100, threshold 80) → `MetricDefinitionCreated` version 1, `UpdateMetricDefinition` weight 0.5 → version 2 retained, `asOf` query returns version 1, duplicate code or weight <0 returns `Error.Validation`.

### Tests for User Story 1 (write FIRST, ensure FAIL)

- [ ] T014 [P] [US1] Unit test `MetricDimensionEnumerationTests` in `tests/Metrics.Tests/Unit/MetricDimensionEnumerationTests.cs` — 10 seeded dimensions resolve, unknown throws
- [ ] T015 [P] [US1] Unit test `MetricWeightTargetThresholdTests` in `tests/Metrics.Tests/Unit/MetricValueObjectTests.cs` — weight <0 / >1 rejected, target 101 rejected, threshold out-of-range rejected
- [ ] T016 [P] [US1] Unit test `MetricDefinitionAggregateTests` in `tests/Metrics.Tests/Unit/MetricDefinitionAggregateTests.cs` — `MetricDefinition.Create` + `Update creates new version` in-memory (append, IsCurrent flip, EffectiveFrom)
- [ ] T017 [P] [US1] Integration test `MetricDefinitionVersionHistoryTests` in `tests/Metrics.Tests/Integration/MetricDefinitionVersionHistoryTests.cs` — Testcontainers Postgres: `DefineMetric` → v1 + outbox `MetricDefinitionCreatedIntegrationEvent`, `UpdateMetricDefinition` → v2 row (`code+project+version` unique), duplicate code same project → 400, `GET ?asOf=2026-08-01` returns version 1
- [ ] T018 [P] [US1] Contract test `MetricsApiContractTests` in `tests/Metrics.Tests/Contract/MetricsApiContractTests.cs` — `POST /api/metrics/definitions` 201 + Location + version 1 per `contracts/metrics-api-contract.md`, `PUT /{id}` 200 version 2 / 409 on stale `expectedVersion`, `GET ?includeHistory` + `?asOf`, `POST /clone` 201

### Implementation for User Story 1

- [ ] T019 [P] [US1] Implement `MetricDefinition` aggregate in `src/Modules/Metrics/Metrics.Domain/Aggregates/MetricDefinition.cs` (`AggregateRoot<MetricDefinitionId>` with `Create(code,name,dimensionId,weight,target,threshold,requiresEvidence,projectId,tenantId)` → `CheckRule(WeightValidRule)` + `Dimension.Exists`, `Update(weight?,threshold?,name?)` → new instance version+1 append, events `MetricDefinitionCreated/Updated`)
- [ ] T020 [P] [US1] Implement `MetricDefinitionCreated/Updated` domain events in `src/Modules/Metrics/Metrics.Domain/Events/MetricsDomainEvents.cs` (`: DomainEvent`) + integration events `MetricDefinitionCreatedIntegrationEvent` in `src/Modules/Metrics/Metrics.Contracts/Events/MetricsIntegrationEvents.cs` (`: IntegrationEvent`)
- [ ] T021 [US1] Implement vertical slices `DefineMetric` in `src/Modules/Metrics/Metrics.Application/Features/Metrics/DefineMetric/` (`DefineMetricCommand(projectId,code,dimension,weight,target,threshold,requiresEvidence,tenantId)` + `Validator` (code `^[a-z0-9_-]+$`, weight 0–1, target/threshold 0–100, dimension in Enumeration) + `Handler` (`IAuthorizationEvaluator.CanActorPerform(metric.define)` subtree check → 403 generic + audit if denied, `MetricDefinition.Create`, `IOutboxWriter.StageAsync(MetricDefinitionCreatedIntegrationEvent)`, `IUnitOfWork.SaveChangesAsync` same tx) + `DefineMetricEndpoint : IEndpoint` `POST /api/metrics/definitions` → `Result.ToCreatedResult`)
- [ ] T022 [US1] Implement `UpdateMetricDefinition` in `src/Modules/Metrics/Metrics.Application/Features/Metrics/UpdateMetricDefinition/` (`UpdateMetricDefinitionCommand(id,weight?,target?,threshold?,expectedVersion)` + `Validator` + `Handler` (append new row `version=max+1`, `IsCurrent` flip, catch `DbUpdateConcurrencyException` → `Error.Conflict`), endpoint `PUT /api/metrics/definitions/{id}`)
- [ ] T023 [US1] Implement `CloneMetricTemplate` slice `src/Modules/Metrics/Metrics.Application/Features/Metrics/CloneMetricTemplate/` (`POST /api/metrics/definitions/clone` per contract) + DTOs `MetricDefinitionResponse` in `src/Modules/Metrics/Metrics.Contracts/Dtos/MetricsDtos.cs` (never domain entities)

**Checkpoint**: US1 independently green — Define → v1, Update → v2 append, history asOf, duplicate 400.

## Phase 4: User Story 2 - Progress computed deterministically and explained (Priority: P1)

**Goal**: Weighted `Σ(progress×weight)/Σweight` over subtasks/deliverables/milestones/evidence/manual, every result persists `ProgressExplanation` (strategy, weightsSum, components, snapshot, zeroWeight, isOverride) byte-identical on same inputs.

**Independent Test**: Parent with 4 weighted subtasks 3×1 at 100% + 1×2 at 0% with `WeightedSubtaskStrategy` → `60%` `(1+1+1+0)/5` with 4 components + strategyId, recompute identical → byte-identical explanation; zeroWeight → `0%` `zeroWeight=true` no crash; manual override → `isOverride=true` audited.

### Tests for User Story 2 (write FIRST)

- [ ] T024 [P] [US2] Unit test `WeightedSubtaskStrategyArithmeticTests` in `tests/Metrics.Tests/Unit/WeightedSubtaskStrategyTests.cs` — 3×100%+1×0% weighted fixture → `60%`, `weightsSum=5`, 4 components with contributions (per spec SC-002)
- [ ] T025 [P] [US2] Unit test `ZeroWeightAndEmptyComponentsTests` in `tests/Metrics.Tests/Unit/ZeroWeightTests.cs` — `Σw==0` or empty components → `0%` `zeroWeight=true` no division-by-zero
- [ ] T026 [P] [US2] Unit test `DeterminismAndExplanationTests` in `tests/Metrics.Tests/Unit/DeterminismTests.cs` — same `InputsSnapshot` + strategy twice → `ProgressExplanation` `ResultPercent/ComponentsJson` byte-identical (SC-001)
- [ ] T027 [P] [US2] Unit test `ManualOverrideExplanationTests` in `tests/Metrics.Tests/Unit/ManualOverrideTests.cs` — `OverrideProgressManually` → explanation `isOverride=true`, component `Manual` dominates, justification persisted
- [ ] T028 [P] [US2] Integration test `ProgressExplanationHistoricalTests` in `tests/Metrics.Tests/Integration/ProgressExplanationHistoricalTests.cs` — Testcontainers: `RecalculateProgress` appends row `(workItemId,computedAt)`, `ExplainProgress(workItemId)` latest vs `ExplainProgress(asOf=T1)` returns T1 version (SC-006), deterministic re-read matches
- [ ] T029 [P] [US2] Contract test `ProgressApiContractTests` in `tests/Metrics.Tests/Contract/ProgressApiContractTests.cs` — `POST /api/progress/{workItemId}/recalculate` 200 `resultPercent 60` per `contracts/progress-api-contract.md`, `GET /explanation?asOf` 200 historical, `POST /override` 200 `isOverride` + `audit.progress.overridden` + 403 without `progress.override`

### Implementation for User Story 2

- [ ] T030 [P] [US2] Implement `ProgressExplanation` append entity + `ComponentValue` record in `src/Modules/Metrics/Metrics.Domain/Entities/ProgressExplanation.cs` (fields `WorkItemId,ProjectId,StrategyId,ComputedAt,ResultPercent,WeightsSum,ZeroWeight,IsOverride,OverrideJustification,OverrideActorId,ComponentsJson(jsonb),InputsSnapshotJson(jsonb),TenantId`; `IsOverride` path)
- [ ] T031 [P] [US2] Implement strategies `WeightedSubtaskStrategy` + `DeliverableMilestoneStrategy` + `IStrategyResolver` in `src/Modules/Metrics/Metrics.Infrastructure/Strategies/` (`ProgressExplanation Calculate(ProgressInputs{SubtaskSnapshots, EvidenceIds, weights})` pure, deterministic ordering, zeroWeight guard)
- [ ] T032 [P] [US2] Implement `ProgressInputs` + `SubtaskSnapshot` records in `src/Modules/Metrics/Metrics.Contracts/Inputs/ProgressInputs.cs` (used by both strategies; `IWorkItemSnapshotProvider` supplies)
- [ ] T033 [US2] Implement vertical slices `RecalculateProgress` in `src/Modules/Metrics/Metrics.Application/Features/Progress/RecalculateProgress/` (`RecalculateProgressCommand(workItemId,tenantId)` + `Handler` (resolve `project.strategyId` → `IStrategyResolver.Get`, `IWorkItemSnapshotProvider.GetSubtasksAsync`, `strategy.Calculate`, append `ProgressExplanation` row, `IOutboxWriter.StageAsync(ProgressRecalculatedIntegrationEvent)` same tx) + `IEndpoint` `POST /api/progress/{workItemId}/recalculate`)
- [ ] T034 [US2] Implement `ExplainProgress` query `src/Modules/Metrics/Metrics.Application/Features/Progress/ExplainProgress/` (`ExplainProgressQuery(workItemId,tenantId,asOf?) : IQuery<Result<ProgressExplanationResponse>>` + `Handler` (`WHERE workItemId AND computedAt <= asOf ORDER BY computedAt DESC LIMIT 1`; tenant check first) + `IEndpoint` `GET /api/progress/{workItemId}/explanation`)
- [ ] T035 [US2] Implement `OverrideProgressManually` in `src/Modules/Metrics/Metrics.Application/Features/Progress/OverrideProgress/` (`OverrideProgressManuallyCommand(workItemId,newProgress 0–100,justification,actorId,expectedVersion)` + `Validator` + `Handler` (`IAuthorizationEvaluator.CanActorPerform(progress.override)` subtree/membership per SPEC-002 → 403 generic + `authorization.denied` audit if denied, `ProgressExplanation` `isOverride=true` append, same-tx outbox `audit.progress.overridden` + `ProgressOverriddenIntegrationEvent`) + `IEndpoint` `POST /api/progress/{workItemId}/override`)
- [ ] T036 [US2] Implement integration events `ProgressRecalculatedIntegrationEvent`, `ProgressOverriddenIntegrationEvent` in `src/Modules/Metrics/Metrics.Contracts/Events/` + DTO `ProgressExplanationResponse` in `src/Modules/Metrics/Metrics.Contracts/Dtos/` (stable `Result` contracts, never entities)
- [ ] T037 [US2] Implement RabbitMQ subscriber `WorkItemStatusChangedHandler : IIntegrationEventHandler<WorkItemStatusChangedIntegrationEvent>` in `src/Modules/Metrics/Metrics.Application/Subscribers/` (trigger `RecalculateProgress` idempotently; also `WorkItemCompleted`) — idempotent via `computedAt` dedup, registers via `AddSubscription<WorkItemStatusChangedIntegrationEvent, WorkItemStatusChangedHandler>` per `AddRabbitMqEventBus`

**Checkpoint**: US2 green — 60% determinism + zeroWeight 0% + historical asOf + override audited isOverride + auto trigger via SPEC-003 event.

## Phase 5: User Story 3 - Deadline status derived and milestone planning verifiable (Priority: P2)

**Goal**: `OnTime|AtRisk|Overdue|CompletedOnTime|CompletedLate` derived pure UTC (`DueDate+WorkItemStatus+atRiskWindowDays`) and `Milestone` dated/verifiable/linked work items versioned, `EvaluateMilestone` → `MilestoneReached/Slipped`.

**Independent Test**: Tasks due tomorrow `InProgress` → `OnTime`, due in 2d with 3d window → `AtRisk`, due yesterday incomplete → `Overdue`, completed before due → `CompletedOnTime`, after due → `CompletedLate`; milestone with 2 linked items all `Completed` → `Reached`, otherwise `Slipped`.

### Tests for User Story 3 (write FIRST)

- [ ] T038 [P] [US3] Unit test `DeadlineEvaluatorBoundaryTests` in `tests/Metrics.Tests/Unit/DeadlineEvaluatorTests.cs` — UTC midnight matrix (tomorrow/onTime, +2 vs 3d window/atRisk, yesterday/overdue, completed before/after due, null dueDate→onTime) per spec edge
- [ ] T039 [P] [US3] Unit test `MilestoneCriteriaTests` in `tests/Metrics.Tests/Unit/MilestoneTests.cs` — linked items all Completed → Reached, one InProgress → Slipped, cross-project linked rejection
- [ ] T040 [P] [US3] Integration test `MilestoneVersionHistoryTests` in `tests/Metrics.Tests/Integration/MilestoneVersionHistoryTests.cs` — Testcontainers: `CreateMilestone` → v1 `Planned`, `UpdateMilestone` dueDate change → v2 append (`IsCurrent` flip), `asOf` query returns version active at date, `EvaluateMilestone` reaches vs slips with events `MilestoneReached/Slipped` via outbox
- [ ] T041 [P] [US3] Contract test `PlanningApiContractTests` in `tests/Metrics.Tests/Contract/PlanningApiContractTests.cs` — `POST /api/planning/milestones` 201 per `contracts/planning-api-contract.md`, cross-project linked 400, `PUT /{id}` versioned, `POST /{id}/evaluate` 200 Reached/Slipped, `GET /deadline?workItemId=&now=` 200 `statusId` 1..5

### Implementation for User Story 3

- [ ] T042 [P] [US3] Implement `Milestone` aggregate in `src/Modules/Metrics/Metrics.Domain/Aggregates/Milestone.cs` (`AggregateRoot<MilestoneId>` with `Create(projectId,title,dueDate,linkedWorkItemIds,criteria,tenantId)` → `CheckRule(TitleRequired)` + `CheckRule(CrossProjectNotAllowed)` via snapshot provider later, version 1 `Planned`; `Update` → new row version+1 append; events `MilestoneCreated/Reached/Slipped`)
- [ ] T043 [P] [US3] Implement pure `DeadlineEvaluator` in `src/Modules/Metrics/Metrics.Infrastructure/Services/DeadlineEvaluator.cs` (`DeadlineStatus Evaluate(DateTime? dueDate,int statusId,DateTime? completedAt,int atRiskWindowDays,DateTime nowUtc)` per research Decision 2, UTC Date truncation)
- [ ] T044 [US3] Implement vertical slices `CreateMilestone` + `UpdateMilestone` in `src/Modules/Metrics/Metrics.Application/Features/Planning/CreateMilestone/` + `UpdateMilestone/` (Validator title 3–100, Validator linked must be in same `ProjectId` via `IWorkItemSnapshotProvider.GetProjectId`, Handler `MetricDefinition`? → `Milestone.Create`, append row, `IOutboxWriter.StageAsync(MilestoneCreatedIntegrationEvent)` same tx, `IEndpoint` `POST /api/planning/milestones` / `PUT /{id}`)
- [ ] T045 [US3] Implement `EvaluateMilestone` in `src/Modules/Metrics/Metrics.Application/Features/Planning/EvaluateMilestone/` (`EvaluateMilestoneCommand(milestoneId,tenantId)` + `Handler` (fetch milestones `IsCurrent`, fetch linked `WorkItem` statuses via snapshot, if all linked `Completed` + evidence approved → `MilestoneReached` else `Slipped` with remainingIds, stage same-tx outbox) + `IEndpoint` `POST /api/planning/milestones/{id}/evaluate`)
- [ ] T046 [US3] Implement `EvaluateDeadlineQuery` in `src/Modules/Metrics/Metrics.Application/Features/Planning/EvaluateDeadline/` (`IQuery<Result<DeadlineStatusResponse>>` → calls `IDeadlineEvaluator`, tenant check) + `IEndpoint` `GET /api/planning/deadline`
- [ ] T047 [US3] Implement `DeadlineStatus` Enumeration + `MetricDimension` historical handling for `dimension=Unknown` (skip) in `Handle unknown dimension` branch `src/Modules/Metrics/Metrics.Domain/Enumerations/MetricDimensions.cs`

**Checkpoint**: US3 green — deadline pure UTC boundaries + milestone Reached only when all linked Completed + version history.

## Phase 6: User Story 4 - Manager dashboards subtree-filtered with violations visible (Priority: P2)

**Goal**: `GetProjectHealth` + `GetManagerDashboard` read models — totals/active/overdue/atRisk/blocked/tasksBySubordinate/completion%/critical/upcomingDeadlines/projectHealth/metric violations — all aggregated after `IManagementHierarchy.GetSubtree` (Golden Rule A) before fetch.

**Independent Test**: Seed two managers with disjoint subtrees, each owning projects, then `GetManagerDashboard(managerA)` aggregates only A's subtree-visible projects (totals, overdue, blocked, by-subordinate, health + violations appear), `managerB` returns disjoint counts (SC-005), violation appears in both health+dashboard for visible viewer.

### Tests for User Story 4 (write FIRST)

- [ ] T048 [P] [US4] Unit test `DashboardAggregationTests` in `tests/Metrics.Tests/Unit/DashboardAggregationTests.cs` — in-memory allowedProjectIds set → totals filtered before GroupBy, never post-filtered; tenant mismatch → 0
- [ ] T049 [P] [US4] Integration test `ManagerDashboardSubtreeTests` in `tests/Metrics.Tests/Integration/ManagerDashboardSubtreeTests.cs` — Testcontainers Postgres + `NSubstitute IManagementHierarchy` (A sees {Alice,Bob} projects, B sees {Carol} disjoint) → `GetManagerDashboard(A)` totals exclude B's, `tasksBySubordinate` grouped, `GetProjectHealth(projectA)` violation visible, `projectB` not included
- [ ] T050 [P] [US4] Integration test `MetricThresholdViolationBothModelsTests` in `tests/Metrics.Tests/Integration/MetricThresholdViolationBothModelsTests.cs` — set `MetricDefinition` threshold 80%, `MetricValue` 62% `IsViolated=true` + `MetricThresholdViolated` via outbox → both `GetProjectHealth` and `GetManagerDashboard` for visible viewer contain `violations[]` with same `definitionId` (SC-003)
- [ ] T051 [P] [US4] Contract test `DashboardsContractTests` in `tests/Metrics.Tests/Contract/DashboardsContractTests.cs` — `GET /api/metrics/project-health?projectId=` 200 health envelope per `contracts/dashboards-contract.md` (or 404 when not visible), `GET /api/dashboards/manager?managerId=` 200 with `violations` + `tasksBySubordinate` subtree-filtered

### Implementation for User Story 4

- [ ] T052 [P] [US4] Implement `IMetricEvaluationPolicy` in `src/Modules/Metrics/Metrics.Infrastructure/Services/MetricEvaluationPolicy.cs` (`MetricValue Evaluate(MetricDefinition def, decimal value)` → `IsViolated = value < threshold` (polarity per dimension), emits `MetricThresholdViolated`)
- [ ] T053 [US4] Implement `MetricValue` aggregate in `src/Modules/Metrics/Metrics.Domain/Aggregates/MetricValue.cs` (`MetricValue.Create(definitionId,projectId,value,threshold,tenantId)` → `IsViolated` + event `MetricThresholdViolated`)
- [ ] T054 [US4] Implement read slices `GetProjectHealth` in `src/Modules/Metrics/Metrics.Application/Features/Dashboards/GetProjectHealth/` (`GetProjectHealthQuery(projectId,tenantId)` + `Handler` (`IAuthorizationEvaluator.CanActorPerform(projectHealth.read)` gated by subtree/membership per SPEC-002 → 404 generic if not visible, then `WorkItem` countable via `IWorkItemSnapshotProvider`? or direct `MetricsDbContext`? Actually `MetricsDbContext` has denormalized `total`? For 004, health aggregates via `IWorkItemSnapshotProvider.GetByProjectId` + `MetricValue` join `WHERE isViolated`, deadline via `IDeadlineEvaluator` for upcoming) + `IEndpoint` `GET /api/metrics/project-health?projectId=`)
- [ ] T055 [US4] Implement `GetManagerDashboard` in `src/Modules/Metrics/Metrics.Application/Features/Dashboards/GetManagerDashboard/` (`GetManagerDashboardQuery(managerId,tenantId)` + `Handler` (resolve `allowedProjectIds = IsInSubtree(managerId, userId) ? subtreeProjects : membershipProjects` via `IManagementHierarchy.GetSubtreeAsync` + `IProjectMembership` thin read, filter `WorkItem|MetricValue|Milestone` by `ProjectId IN allowedSet AND TenantId` **before** `GroupBy/Count/Avg`, tenant first) + `IEndpoint` `GET /api/dashboards/manager?managerId=`)
- [ ] T056 [US4] Implement DTOs `ProjectHealthResponse`, `ManagerDashboardResponse`, `TasksBySubordinateDto` in `src/Modules/Metrics/Metrics.Contracts/Dtos/DashboardDtos.cs` (stable `Result` contracts, never entities) + integration events `MetricThresholdViolatedIntegrationEvent`, `MilestoneReachedIntegrationEvent` in `src/Modules/Metrics/Metrics.Contracts/Events/`
- [ ] T057 [US4] Implement `ProjectMetricsSettings` per-project `strategyId` + `atRiskWindowDays` read via `src/Modules/Metrics/Metrics.Infrastructure/Persistence/ProjectSettingsConfiguration.cs` (already used by strategy resolver + deadline evaluator)

**Checkpoint**: US4 green — managerA/B disjoint totals, health shows violations only when visible, no post-filter leak.

## Phase 7: User Story 5 - Historical and audited progressive insight (Priority: P3)

**Goal**: Manual override audited (`audit.progress.overridden` actor+previous+justification via same-tx outbox) and historical `ExplainProgress(asOf)` reconstructible from persisted explanations.

**Independent Test**: `OverrideProgressManually` → audit entry `audit.progress.overridden` + next `ExplainProgress` `isOverride=true` with justification; `ExplainProgress(asOf=2026-08-01)` returns explanation active at that date (SC-004/006), not latest.

### Tests for User Story 5 (write FIRST)

- [ ] T058 [P] [US5] Unit test `OverrideAuditedTests` in `tests/Metrics.Tests/Unit/OverrideAuditedTests.cs` — `OverrideProgressManually` justification persists in `ProgressExplanation` `isOverride` + `InputsSnapshot`
- [ ] T059 [P] [US5] Integration test `HistoricalReconstructionTests` in `tests/Metrics.Tests/Integration/HistoricalReconstructionTests.cs` — Testcontainers: append two explanations T1/T2 for same `workItemId`, `ExplainProgress(asOf=T1+offset)` returns T1 (not T2), ordered by computedAt
- [ ] T060 [P] [US5] Contract test `OverrideProgressContractTests` in `tests/Metrics.Tests/Contract/OverrideProgressContractTests.cs` — `POST /api/progress/{workItemId}/override` 200 `isOverride` + 403 when `progress.override` denied per Golden Rule A (audited `authorization.denied`), `GET /explanation?asOf` 200 historical

### Implementation for User Story 5

- [ ] T061 [US5] Implement `OverrideProgressManually` already in US2 — extend handler to persist `OverrideJustification/OverrideActorId` in `ProgressExplanation` + stage same-tx outbox `AuditEntry` `audit.progress.overridden` (`actor, previous, new, justification, correlationId`) — already in T035, now ensure historical index covers override rows
- [ ] T062 [US5] Ensure `ExplainProgressQuery` handles historical `asOf` in `src/Modules/Metrics/Metrics.Application/Features/Progress/ExplainProgress/` (`WHERE workItemId AND computedAt <= asOf ORDER BY computedAt DESC LIMIT 1`, tenant check; no recompute from live state)
- [ ] T063 [US5] Implement audit Outbox → `Audit` BC integration event `AuditIntegrationEvent` via `IOutboxWriter.StageAsync(AuditIntegrationEvent)` same tx (append-only, never overwrite history)

**Checkpoint**: US5 green — override audited with justification, `asOf` reconstructs historical explanation.

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Wiring, security hardening, docs, performance, dashboard UX.

- [ ] T064 Implement dashboard store `src/Web/src/app/features/dashboard/dashboard.store.ts` (`signalStore(withState({projectHealth,managerDashboard}), withRequestStatus(), withLogger('dashboard'), withSelectedEntity(), withComputed({filtered}))`) per `ngrx-signal-store` skill + dashboard component `src/Web/src/app/features/dashboard/dashboard.component.ts` (KPI cards elevated `0 8px 24px`, requestStatus pending/error/fulfilled)
- [ ] T065 Add component specs `src/Web/src/app/features/dashboard/dashboard.store.spec.ts` + `dashboard.component.spec.ts` (`provideHttpClientTesting`, `HttpTestingController`, `setPending/setFulfilled/setError` patchState)
- [ ] T066 Add/extend `tests/Architecture/ArchitectureTests.cs` in `tests/Architecture/ArchitectureTests.cs` — boundary guard: `Metrics.Domain` has no ref to `Projects.Infrastructure`/`Organization.Infrastructure` (only `Metrics.Contracts` + `Organization.Contracts` via `IManagementHierarchy`), `Metrics.Application` refs only `Metrics.Domain`/`Contracts`/`Organization.Contracts` (no direct `ProjectsDbContext`); `ProgressExplanation` append-only (no `UPDATE` on history)
- [ ] T067 Performance smoke in `tests/Metrics.Tests/Performance/MetricsPerformanceTests.cs` — seed 100 tasks across 5 subordinates, `GetProjectHealth` <200ms, `GetManagerDashboard` <500ms p95 via EF `GroupBy` (SC-007 via strategy <100ms)
- [ ] T068 Run `specs/004-metrics-progress-planning/quickstart.md` end-to-end (6-pillar curl) — metric version history, 60% weighted, zeroWeight 0%, override audited, deadline OnTime/AtRisk/Overdue/Completed*, violation→both read models, subtree disjoint, historical asOf
- [ ] T069 [P] Documentation: update `docs/api/README.md` + `docs/adr/adr-00x-metrics-progress.md` (strategy factory, deadline UTC midnight, append versioning) and `specs/004-metrics-progress-planning/contracts/` change log; ensure `IProgressExplanationReader` ADR recorded
- [ ] T070 Code cleanup + `dotnet format OroKanban.slnx` + final gate `dotnet build OroKanban.slnx -warnaserror` 0 warnings + `dotnet test tests/Metrics.Tests -v minimal` + `npm --prefix src/Web test` green

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories (MetricsDbContext, MetricDimension, MetricWeight, IStrategy contracts, IWorkItemSnapshotProvider).
- **User Stories (Phase 3+)**: All depend on Foundational.
  - `US1 (P1)` is prerequisite for `US2` (progress needs definitions) and `US4` (violations need definitions) — start US1 first, US2 can start once `MetricDefinition` aggregate exists.
  - `US2 (P1)` is prerequisite for `US4` (dashboard completion% derived from explanations) and `US5` (historical).
  - `US3 (P2)` depends on Foundational + `IWorkItemSnapshotProvider` (read Projects), independent of US1/US2 except via pending linked items; can parallel with US2 if stubbed.
  - `US4 (P2)` depends on US1 (definitions/violations) + US2 (explanations) + `IManagementHierarchy` stub — schedule after US1/US2 green.
  - `US5 (P3)` depends on US2 (append `ProgressExplanation` + override path) — schedule after US2.
- **Polish (Phase 8)**: Depends on all desired user stories (3–7) complete.

### User Story Dependencies

- **US1 (P1) Metrics versioned**: No dependencies beyond Foundational — is the configuration prerequisite for progress.
- **US2 (P1) Deterministic progress**: Depends on US1's `MetricDefinition` for context but can start in parallel with US1's aggregate skeleton; must integrate its version history once US1 done.
- **US3 (P2) Deadline+Milestone**: Depends on Foundational + `IWorkItemSnapshotProvider`, independent of US1/US2, can parallel with US2 if stubbed WorkItem statuses.
- **US4 (P2) Dashboards subtree**: Depends on US1 (violations) + US2 (completion%) + `IManagementHierarchy` — most integrated; schedule after US1/US2.
- **US5 (P3) Historical/Audited**: Depends on US2's `ProgressExplanation` append + `OverrideProgressManually` path.
- **Constitution**: Every `ExplainProgress` must have persisted explanation (SC-008) — absence is failure; every dashboard must compose subtree `Specification<T>` before fetch (VII).

### Within Each User Story

1. Tests (unit + integration + contract) MUST be written and FAIL before implementation (TDD, Constitution XXI).
2. Aggregates/VOs → domain services (`IProgressCalculationStrategy` pure) → handlers/validators → endpoints.
3. `ProgressExplanation` append before `MAX(computedAt)` query; never `UPDATE` history.
4. `IManagementHierarchy.GetSubtreeAsync` resolved before `MetricsDbContext` aggregation.

### Parallel Opportunities

- Phase 1: `T002` (test project) parallel with `T003` (package refs) + `T004` (AppHost check).
- Phase 2: `T006` (Enumerations) parallel with `T007` (VOs) + `T008` (MetricsDbContext) + `T009` (EF configs).
- Phases 3–7: Once Foundational done, US1 and US2 can be staffed in parallel (different entities: MetricDefinition vs ProgressExplanation/Strategy), US3 can parallel with US2 if `IWorkItemSnapshotProvider` stubbed.
- Within each story: All tests `T014–T018` (US1), `T024–T029` (US2) can run in parallel (different files).

## Parallel Example: User Story 2 (Weighted deterministic progress)

```bash
# Launch all US2 tests in parallel (different files, no dependencies):
Task: "Unit test WeightedSubtaskStrategyArithmeticTests in tests/Metrics.Tests/Unit/WeightedSubtaskStrategyTests.cs" # T024
Task: "Unit test ZeroWeightAndEmptyComponentsTests in tests/Metrics.Tests/Unit/ZeroWeightTests.cs" # T025
Task: "Unit test DeterminismAndExplanationTests in tests/Metrics.Tests/Unit/DeterminismTests.cs" # T026
Task: "Integration test ProgressExplanationHistoricalTests in tests/Metrics.Tests/Integration/ProgressExplanationHistoricalTests.cs" # T028

# Then all US2 strategies in parallel (after tests fail):
Task: "ProgressExplanation entity in src/Modules/Metrics/Metrics.Domain/Entities/ProgressExplanation.cs" # T030
Task: "WeightedSubtaskStrategy + DeliverableMilestoneStrategy in src/Modules/Metrics/Metrics.Infrastructure/Strategies/" # T031
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 Setup (`T001–T004`) → `dotnet build` green.
2. Complete Phase 2 Foundational (`T005–T013`) → `metrics` schema + `MetricDimension` seeded.
3. Complete Phase 3 US1 (`T014–T023`) → `DefineMetric` v1, `UpdateMetricDefinition` v2 append, history `asOf`, duplicate 400.
4. **STOP and VALIDATE**: `dotnet test --filter US1` green + `POST /api/metrics/definitions` → 201 version 1 → `PUT` → version 2 historical query.
5. Deploy/demo if ready — US1 alone delivers versioned configurable metrics (Principle XIII).

### Full P1 (US1+US2 — Explainable progress MVP)

1. Above, then add Phase 4 US2 (`T024–T037`) — weighted `60%` determinism + zeroWeight `0%` + historical `asOf` + manual override audited `isOverride` + auto trigger via `WorkItemStatusChanged` event.
2. Validate: `ExplainProgress` byte-identical on same snapshot + `GET /explanation?asOf` historical (SC-001/002/006).
3. This is the recommended MVP for external demo — metrics + explainable progress.

### Incremental Delivery (P2, P3)

1. Add Phase 5 US3 → `DeadlineEvaluator` pure UTC midnight + `Milestone` versioned Reached/Slipped.
2. Add Phase 6 US4 → `MetricEvaluationPolicy` violation → both `GetProjectHealth` + `GetManagerDashboard` with subtree disjoint.
3. Add Phase 7 US5 → historical reconstructibility + override audit via same-tx outbox.
4. Each increment is independently testable and deployable without breaking prior stories.

### Parallel Team Strategy

With 3 developers after Phase 2:

- Dev A: US1 (Phase 3) + US2 (Phase 4) — owns `MetricDefinition` versioning + `IProgressCalculationStrategy` determinism.
- Dev B: US3 (Phase 5) — owns `IDeadlineEvaluator` pure + `Milestone` versioned evaluation.
- Dev C: US4 (Phase 6) + US5 (Phase 7) — owns `GetProjectHealth`/`GetManagerDashboard` subtree aggregation + historical `ExplainProgress`.

US5 (Phase 7) is the integration gate — team syncs to wire historical `asOf` + audit path across all slices.

## Notes

- **Constitution traceability**: VI (domain rules `T021/T035/T043`), VII (subtree `T054/T055` before `GroupBy`), VIII (append-only audit `T035` `audit.progress.overridden` + threshold `MetricThresholdViolated`), XII (`ProgressExplanation` persisted, never arbitrary `T030/T034`), XIII (`MetricDimension` seedable `T006/T014`), XV (tenant `T021/T033`), XVI (stable `Result` contracts `T023/T036`), XX/XXI (TDD `T014 first FAIL` + `withRequestStatus`/`withLogger` dashboard `T064`).
- `[P]` = different files, no dependency on incomplete tasks — can be parallelized.
- `[US#]` maps task to specific user story for traceability (setup/foundational/polish have no story label per required format).
- File paths are absolute-from-repo-root (e.g., `src/Modules/Metrics/Metrics.Domain/Aggregates/MetricDefinition.cs`).
- FR-010: any new file/project via platform CLIs (`dotnet new classlib` style) — not manual copy — where applicable.
- Avoid vague tasks, same-file conflicts, or cross-story dependencies that break independent testability.

## Phase 9: Convergence

- [X] T071 Create distinct unit tests for Metrics per Constitution XX - WeightedSubtaskStrategy/ZeroWeight/Determinism/DeadlineBoundary per TDD (missing) `Constitution XX` (missing) CRITICAL
- [ ] T072 Implement MetricDefinition versioned append + MetricDimension Enumeration per FR-001/002 (missing) `FR-001` (missing)
- [ ] T073 Implement IProgressCalculationStrategy weighted Σ(w×p)/Σw + ProgressExplanation deterministic zeroWeight per FR-003/004 (missing) `FR-003` (missing)
- [ ] T074 Implement IDeadlineEvaluator pure UTC OnTime|AtRisk|Overdue|Completed* per FR-006 (missing) `FR-006` (missing)
- [ ] T075 Implement Milestone versioned append + EvaluateMilestone Reached/Slipped per FR-007/008 (missing) `FR-007` (missing)
- [ ] T076 Implement GetProjectHealth/GetManagerDashboard subtree-filtered before fetch via IManagementHierarchy per FR-009 (missing) `FR-009` (missing)
- [ ] T077 Implement IMetricEvaluationPolicy MetricThresholdViolated visible in both read models per FR-010 (missing) `FR-010` (missing)
- [ ] T078 Implement ExplainProgress(asOf) historical reconstructibility append-only per FR-011 (missing) `FR-011` (missing)
