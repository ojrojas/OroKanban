# Research: Metrics, Progress and Planning

**Feature**: 004-metrics-progress-planning | **Date**: 2026-09-01 | **Status**: Complete

No `NEEDS CLARIFICATION` remained — all technical choices are informed by `draft/libraries/buildingblocks.md` + `draft/discovery/000-repository-catalog.md` + BC-03 reuse.

## Decision 1: Progress strategy — pluggable IProgressCalculationStrategy with weighted Σ(w×p)/Σw

- **Decision**: `IProgressCalculationStrategy { string StrategyId; ProgressExplanation Calculate(ProgressInputs inputs) }` with two injectable implementations: `WeightedSubtaskStrategy` (components = child WorkItems weighted by subtask priority/weight) and `DeliverableMilestoneStrategy` (components = deliverables/milestones hit/evidence). Selection stored per `Project.progressStrategyId` (`MetricsDbContext.projects_settings` or `MetricsDbContext.ProjectsStrategy` table) and resolved via `IStrategyResolver` (factory map). Handlers call `strategy.Calculate(inputs)` → `ProgressExplanation` → append to `progress_explanations`. Standalone updater-friendly: strategies are pure, deterministic, no I/O.
- **Rationale**: Satisfies R2/R3 + Principle XII — deterministic, testable without infrastructure, per-project configurability without aggregate change. Pure function enables byte-identical re-run via same `InputsSnapshot`.
- **Alternatives considered**: Single hard-coded strategy (rejected — violates per-project configurability), trigger in DB function (rejected — violates VI, not unit-testable), ML-based progress (rejected — overkill, violates XII explainability).
- **Edge**: `Σw == 0` → `result 0%` + `zeroWeight=true` in explanation, never throw (SC-002).

## Decision 2: Deadline semantics — pure UTC IDedlineEvaluator, atRiskWindowDays per project

- **Decision**: `IDeadlineEvaluator { DeadlineStatus Evaluate(DateTime? dueDate, int statusId, int atRiskWindowDays, DateTime nowUtc) }` pure (no DB). UTC midnight truncation: `dueDate.Date` vs `nowUtc.Date`. Rules: if `status==Completed` → `CompletedOnTime` when `completedAt <= dueDate.Date` else `CompletedLate`; else if `dueDate==null` → `OnTime`; else if `dueDate.Date < nowUtc.Date` → `Overdue`; else if `(dueDate.Date - nowUtc.Date).Days <= atRiskWindowDays` → `AtRisk`; else `OnTime`. Default `atRiskWindowDays=3` per `ProjectMetricsSettings` (row in `metrics.project_settings`). ValueObject/Enumeration `DeadlineStatus {OnTime(1),AtRisk(2),Overdue(3),CompletedOnTime(4),CompletedLate(5)}`.
- **Rationale**: R4 + timezone correctness; pure function is trivially unit-tested across midnight boundaries (spec edge).
- **Alternatives**: UI string mapping (rejected — violates R4), per-timezone evaluation (rejected — spec says UTC).

## Decision 3: Versioning — append-only metric definitions, milestones, explanations

- **Decision**: `MetricDefinition` and `Milestone` are versioned via **append rows** (`code+projectId+version` unique, `isCurrent` bool or `version DESC` as current). Update creates `version+1` row copying previous fields + `EffectiveFrom=UtcNow`, never `UPDATE` history. `ProgressExplanation` is append per computation (`workItemId, computedAt` PK, plus `version` per workItem). Queries: current via `WHERE isCurrent=true` or `MAX(version)`; historical via `WHERE version == or computedAt <= asOf ORDER BY computedAt DESC LIMIT 1`. `RowVersion` concurrency only on current mutable rows. EF config: `HasIndex(p=>new{p.ProjectId,p.Code,p.Version}).IsUnique()`, `HasIndex(e=>new{e.WorkItemId,e.ComputedAt})`.
- **Rationale**: Satisfies R1/R5/FR-014 + §Versioning + `ExplainProgress(asOf)` reconstructibility (SC-006). Append-only matches `Audit` principle VIII.
- **Alternatives**: In-place `UPDATE` + audit log (rejected — loses direct version queryability), event-sourcing (rejected — over-engineering for BC-04 scope).

## Decision 4: Dashboards — EF read model with IManagementHierarchy before aggregation

- **Decision**: `GetProjectHealth`/`GetManagerDashboard` handlers resolve `allowedProjectIds = IsInSubtree(managerId) ? subtreeProjectIds : explicit membershipProjectIds` via `IManagementHierarchy.GetSubtreeAsync` + `IProjectMembership` (read of `metrics`/`projects`? For dashboards, project visibility = manager is in project’s manager subtree or is member). Then `IQueryable<WorkItem/ MetricValue>` filtered by `ProjectId IN allowedSet AND TenantId==ctx.TenantId` **before** `GroupBy/Count/Avg`. Metric violation join: `MetricValue.IsViolated` → `MetricThresholdViolated` events already persisted, so dashboards just project `WHERE isViolated`. Metric definitions not needed at query time except for health metadata. Tenant is first predicate.
- **Rationale**: R6 + Golden Rule A + VII — never post-filter aggregation; same pattern as BC-03 board. Tenant-first ensures isolation.
- **Alternatives**: Post-filter in memory (rejected — leaks cross-branch, violates VII), separate analytics DB (rejected — premature for ≤100 tasks scope).

## Decision 5: Triggering — on-demand + EventBus subscriber to workitem.* (idempotent)

- **Decision**: Progress recalc triggered two ways: (1) `POST /metrics/progress/{workItemId}/recalculate` (vertical slice `RecalculateProgressCommand` → `IProgressCalculationStrategy` → `ProgressExplanation` append) and (2) `IIntegrationEventHandler<WorkItemStatusChangedIntegrationEvent>` + `WorkItemCompletedIntegrationEvent` (from SPEC-003 `integration_events` topic via RabbitMQ `IEventBus` + `OutboxProcessor`). Handlers fetch latest `InputsSnapshot` (via `IWorkItemSnapshotProvider` reading `ProjectsDbContext` read-only), call strategy, persist explanation with `computedAt`, handle `DbUpdateConcurrencyException` by re-reading current and recomputing (idempotent). Handlers are `IHostedService` background consumer via `AddRabbitMqEventBus().AddSubscription<...>`. No Redis needed.
- **Rationale**: R2/R3 auto + manual paths + XVII async + idempotent at-least-once (same inputs → same output).
- **Alternatives**: Polling cron only (rejected — misses real-time), direct cross-module DbContext call without event (rejected — violates V modularity).

## Decision 6: Storage — single MetricsDbContext schema `metrics` + thin WorkItem snapshot adapter

- **Decision**: `MetricsDbContext : AppDbContextBase` with `HasDefaultSchema("metrics")` owns `metric_definitions`, `metric_definitions_history` (if version rows split), `milestones`, `milestone_work_items`, `progress_explanations`, `metric_values`. WorkItem data for strategy comes via `IWorkItemSnapshotProvider { Task<IReadOnlyList<SubtaskSnapshot>> GetSubtasksAsync(workItemId, ct)}` implemented in `Metrics.Infrastructure` reading `ProjectsDbContext` (injected via `IServiceProvider` scope) — narrow read-only projection, no cross-schema FK. TenantId added to every Metrics table for isolation.
- **Rationale**: Keeps BC-04 persistence isolated (V) while satisfying strategy input needs without duplicating work items. No new Aspire resources; reuses `postgres` logical DB.
- **Alternatives**: Direct FK to `projects.work_items` (rejected — cross-schema FK violates modular boundary), duplicate work items table (rejected — consistency burden).
