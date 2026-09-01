# Feature Specification: Metrics, Progress and Planning

**Feature Branch**: `004-metrics-progress-planning`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "SPEC-004 — Metrics, Progress and Planning **Bounded Context**: BC-04 Metrics & Progress (Core) · **Depends on**: SPEC-003 **Objective**: Make progress measurable, configurable, deterministic, and explainable — never an arbitrary number (Constitution Principle XII). **Requirements**: R1 Configurable metric definitions (MetricDefinition per project/template, dimension, weight, target, threshold, evidence requirement, version-aware), R2 Progress strategy Σ(componentProgress × weight)/Σ(weight) with pluggable IProgressCalculationStrategy, manual override audited/permissioned, R3 Explainability with persisted ProgressExplanation reconstructible, R4 Deadline semantics OnTime|AtRisk|Overdue|CompletedLate|CompletedOnTime as VOs, R5 Planning Milestone dated/verifiable/linked to work items, version-aware, R6 Manager dashboards subtree-filtered via IManagementHierarchy (Golden Rule A). **Domain Model**: MetricDefinition, MetricValue, Milestone aggregates; VOs MetricDimension, MetricWeight, MetricTarget, MetricThreshold, DeadlineStatus, ProgressExplanation; services IProgressCalculationStrategy, IMetricEvaluationPolicy, IDeadlineEvaluator. **Application**: DefineMetric, UpdateMetricDefinition, OverrideProgressManually, CreateMilestone, EvaluateMilestone, Queries GetProjectHealth, GetManagerDashboard, ExplainProgress. **Acceptance**: determinism, weighted subtasks explanation, threshold violation → MetricThresholdViolated visible, manual override audited, dashboard subtree-filtered, historical reconstructible. **TDD**: unit strategies/weights/deadlines/determinism, integration via SPEC-003 events + dashboards. **Traceability**: Principles XII, XIII; §Versioning."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Project defines and versions configurable metrics (Priority: P1)

As a project manager, I want to define metrics per project or template (dimension, weight, target, threshold, evidence requirement) and update them with versioning, so that what counts as progress is explicit, comparable, and traceable over time.

**Why this priority**: Without configurable definitions, progress reverts to arbitrary numbers (violates XII/XIII). This story unlocks R1 and is prerequisite for strategy, thresholds, and dashboards. P1 because all progress computation depends on it.

**Independent Test**: Can be fully tested by calling `DefineMetric` (code=delivery-date, dimension=DeadlineAdherence, weight=0.3, target=100%, threshold=80%, evidenceRequired=false) → verify `MetricDefinitionCreated` + version 1 in outbox/audit, then `UpdateMetricDefinition` (weight=0.5) → version 2 persisted with history, old version still queryable, and a metric with invalid weight (<0 or >1) returns validation error.

**Acceptance Scenarios**:

1. **Given** a project with no metrics, **When** `DefineMetric` is called with valid code, name, dimension, weight, target, threshold, evidence flag, **Then** a `MetricDefinition` is persisted version 1, `MetricDefinitionCreated` emitted via outbox, and the definition appears in `GetProjectHealth` metadata.
2. **Given** an existing metric version 1, **When** `UpdateMetricDefinition` changes weight or threshold, **Then** a new version 2 is created (append, not overwrite), `MetricDefinitionUpdated` emitted, and historical queries return the correct version for the given date.
3. **Given** a definition per template, **When** a new project clones the template, **Then** metric definitions are copied as version 1 for the new project and diverge independently.
4. **Given** an invalid definition (duplicate code in same project, weight negative, target outside 0–100, unknown dimension), **When** `DefineMetric` executes, **Then** it returns `Error.Validation` and no metric is persisted.

---

### User Story 2 - Progress computed deterministically and explained (Priority: P1)

As a team member or manager, I want progress computed by a pluggable weighted strategy (`Σ(progress×weight)/Σweight`) over subtasks/deliverables/milestones/evidence/manual values, with every result persisted as an explanation showing components, weights, arithmetic, and snapshot, so that the number is reproducible and auditable.

**Why this priority**: Directly implements Principle XII (progress never arbitrary) + R2/R3. Determinism and explainability are the highest-value domain invariants; without them dashboards mislead. P1 alongside US1.

**Independent Test**: Can be fully tested by seeding a parent task with 4 weighted subtasks (3 complete at 100%, 1 at 0% with weights 1,1,1,2) under `WeightedSubtaskStrategy` → compute → progress = (1+1+1+0)/5 = 60%, then call `ExplainProgress(workItemId)` → response lists 4 components with weights/values and `60%` and recomputing with same inputs returns identical value and explanation (determinism).

**Acceptance Scenarios**:

1. **Given** identical inputs (same subtask states, weights, evidence list, milestone hits), **When** progress is recalculated twice, **Then** `ProgressExplanation` values are byte-identical (determinism) and `ExplainProgress` returns the same arithmetic.
2. **Given** a task with 3 of 4 weighted subtasks complete (weights equal), **When** `ExplainProgress` runs, **Then** it returns `components[{name, weight, progress, contribution}]`, `strategyId`, `weightsSum`, `formula`, and `inputsSnapshot` showing each subtask status.
3. **Given** a project with strategy `DeliverableMilestoneStrategy`, **When** computation runs, **Then** components are deliverables/milestones hit/evidence approved instead of subtasks, but the same weighted formula applies and is selectable per project (`IProgressCalculationStrategy`).
4. **Given** zero total weight (all weights 0 or no components), **When** computation runs, **Then** result is `0%` with explanation noting `zeroWeight` and no division-by-zero error.
5. **Given** a manual override via `OverrideProgressManually` with justification, **When** evaluated, **Then** the component `Manual` with the override value dominates per policy, is marked `isOverride=true`, audited with actor+justification, and included in the explanation as the source.

---

### User Story 3 - Deadline status derived and milestone planning verifiable (Priority: P2)

As a planner, I want deadlines evaluated as `OnTime | AtRisk | Overdue | CompletedLate | CompletedOnTime` from dates+status (as VOs, not UI strings) and milestones that are dated, linked to work items, and explicitly evaluated as `MilestoneReached`/`MilestoneSlipped`, so that planning is version-aware and objective.

**Why this priority**: R4/R5. Deadline semantics and verifiable milestones give planning its teeth, but depend on US1/US2's metric/progress foundation. P2.

**Independent Test**: Can be fully tested by creating milestones (due 2026-10-15 linked to 2 work items) and tasks with due dates in past/future and statuses `InProgress`/`Completed`, then calling `EvaluateMilestone` and `IDeadlineEvaluator` → OnTime/AtRisk/Overdue transitions match date boundaries, `CompletedOnTime` vs `CompletedLate` matches completion vs due, and `MilestoneReached` only when linked criteria pass.

**Acceptance Scenarios**:

1. **Given** a task due tomorrow and `InProgress`, **When** deadline is evaluated today, **Then** status is `OnTime`; when due in 2 days and incomplete with at-risk window = 3 days, it is `AtRisk`; when due yesterday and incomplete, it is `Overdue`.
2. **Given** a task completed before due date, **When** evaluated, **Then** `CompletedOnTime`; completed after due date → `CompletedLate`.
3. **Given** a milestone linked to work items, **When** `EvaluateMilestone` runs and all linked items are `Completed` and verification evidence approved, **Then** `MilestoneReached` emitted; otherwise `MilestoneSlipped` with remaining items listed.
4. **Given** a milestone date change, **When** updated, **Then** a new plan version is created (append) and previous version remains reconstructible for historical queries.

---

### User Story 4 - Manager dashboards subtree-filtered with violations visible (Priority: P2)

As a manager, I want a dashboard of totals, active, overdue, blocked, tasks by subordinate, completion %, critical, upcoming deadlines, project health, and metric violations — all filtered by `IManagementHierarchy` (Golden Rule A subtree/membership), so I see only what I am allowed to see and violations are actionable.

**Why this priority**: R6. This is the user-visible payoff of metrics/progress, but it requires US1–US3 data and SPEC-002 `IManagementHierarchy`. P2; depends on hierarchy.

**Independent Test**: Can be fully tested by seeding two managers each with subordinates and projects, then calling `GetManagerDashboard(managerA)` → only managerA's subtree/membership-visible projects contribute (totals, overdue, blocked, by-subordinate, health), metric threshold violations for those projects appear, and managerB's query returns disjoint counts (subtree isolation).

**Acceptance Scenarios**:

1. **Given** a hierarchy where manager A supervises Alice/Bob and manager B supervises Carol, **When** `GetManagerDashboard(A)` executes, **Then** counts (totals, active, overdue, blocked, critical) aggregate only tasks whose project is visible via `IManagementHierarchy` subtree or project membership, not B's projects.
2. **Given** a metric threshold violated (e.g., completion % < threshold), **When** `IMetricEvaluationPolicy` runs, **Then** `MetricThresholdViolated` is emitted and the violation appears in `GetProjectHealth` and `GetManagerDashboard` for viewers with visibility.
3. **Given** a dashboard query, **When** the user lacks subtree/membership visibility to a project, **Then** that project's data never contributes (filtered before aggregation, never post-filtered).

---

### User Story 5 - Historical and audited progressive insight (Priority: P3)

As an auditor or manager, I want manual overrides audited (actor, justification, previous value) and historical progress reconstructible from persisted explanations, so that changes are traceable and past states can be answered.

**Why this priority**: Satisfies auditability (VIII) and versioning, and the acceptance criterion "Given a historical date, progress can be reconstructed". P3 after core computation and dashboards.

**Independent Test**: Can be fully tested by performing a manual override (`OverrideProgressManually` with justification) → verify audit entry with `audit.progress.overridden`, then query `ExplainProgress(workItemId, asOf=2026-08-01)` → returns the explanation version active at that date, matching the earlier recomputation.

**Acceptance Scenarios**:

1. **Given** a manual progress override with justification, **When** saved, **Then** the override, actor `sub`, justification, previous progress, and new progress are audited via outbox and appear in the next `ExplainProgress` as `isOverride=true`.
2. **Given** two past explanations stored at T1 and T2, **When** `ExplainProgress(asOf=T1)` is queried, **Then** the value and components from T1 are returned, not the latest T2 value.
3. **Given** any progress recalculation, **When** persisted, **Then** `ProgressExplanation` includes `strategyId`, `weightsSum`, `components`, `inputsSnapshot` (subtask statuses, evidence IDs) so an independent recomputation can reproduce the value.

---

### Edge Cases

- What happens when a metric dimension is unknown or removed after values exist? Values remain queryable with `dimension=Unknown` and evaluation skips missing definitions with a warning explanation.
- What happens when a weight sum is 0 or all components have weight 0? Result is 0% with `zeroWeight=true` explanation, no crash.
- What happens when due date equals today at midnight boundary? `IDeadlineEvaluator` uses UTC date truncation; `AtRisk` window is `dueDate - now <= atRiskDays && now < dueDate`.
- What happens when a milestone links to work items in different projects? Validation rejects with `Error.Validation` unless strategy allows cross-project milestones (default: reject).
- What happens when manual override permission is insufficient? `OverrideProgressManually` returns `Error.Forbidden` (generic denial, audited as `authorization.denied`) per Golden Rule A.
- What happens when metric thresholds are updated mid-sprint? Existing `MetricValue` explanations remain at computation time; new evaluation uses the new threshold version.
- What happens when deadline evaluation crosses daylight/timezone? All dates stored/normalized to UTC; evaluation uses `DateTime.UtcNow` with no local offset.
- What happens when evidence required but not provided? Metric evaluation marks `needsEvidence` and contributes 0% to that component until approved evidence arrives.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide `MetricDefinition` as `AggregateRoot<MetricDefinitionId>` per project or per template with fields: code (unique per project/template), name, `MetricDimension` (Enumeration), `MetricWeight` (0–1, validated), `MetricTarget` (0–100%), `MetricThreshold` (0–100% violation point), `requiresEvidence` (bool), version, `TenantId`; every change MUST create a new version (append, never overwrite) and emit `MetricDefinitionCreated` / `MetricDefinitionUpdated` via outbox; version history MUST be queryable.
- **FR-002**: `MetricDimension` MUST be an `Enumeration` (e.g., Completion, DeadlineAdherence, ContentCompleteness, Quality, Risk, Criticality, Effort, DependencyHealth, DocumentCompliance, ReviewStatus) — extensible by seeding a row, never by code change to the aggregate.
- **FR-003**: System MUST compute progress via pluggable `IProgressCalculationStrategy` selected per project (e.g., `WeightedSubtaskStrategy`, `DeliverableMilestoneStrategy`): `progress = Σ(componentProgress × componentWeight)/Σ(componentWeight)` where components are weighted subtask completion, deliverables, milestones hit, validation criteria, approved evidence, and optionally manual values. Strategy MUST be configurable per project (stored as `project.strategyId`) and injectable/testable without infrastructure.
- **FR-004**: Every computed progress MUST persist a `ProgressExplanation` (VO or entity) with: `workItemId`, `projectId`, `strategyId`, `computedAt`, `resultPercent`, `weightsSum`, `components[{name, weight, progress, contribution}]`, `inputsSnapshot` (subtask statuses/ids at compute time, evidence IDs), `isOverride` flag; computation MUST be deterministic — same inputs snapshot plus same strategy always yields identical explanation.
- **FR-005**: System MUST support `OverrideProgressManually(workItemId, newProgress, justification, actor)` as a permissioned, audited command (`progress.override` permission via `IAuthorizationEvaluator` subtree/membership per SPEC-002): on success it persists an override component with `isOverride=true`, appends an audit entry (`audit.progress.overridden`, actor, previous, new, justification) via same-tx outbox, and the next `ExplainProgress` includes it as the source.
- **FR-006**: System MUST derive deadline status as `DeadlineStatus` ValueObject/Enumeration (`OnTime`, `AtRisk`, `Overdue`, `CompletedOnTime`, `CompletedLate`) via `IDeadlineEvaluator` from `DueDate` + `WorkItemStatus` (`Completed` vs incomplete) + `atRiskWindowDays` (per project, default 3); evaluation MUST be pure, timezone-UTC, and date-boundary aware; deadline evaluation MUST NOT be a UI string mapping.
- **FR-007**: System MUST provide `Milestone` as `AggregateRoot<MilestoneId>` with fields: `ProjectId`, `Title`, `DueDate`, `Criteria` (verifiable predicate over linked work items + evidence), `LinkedWorkItemIds`, `Status` (`Planned|Reached|Slipped`), `Version`; evaluation via `EvaluateMilestone(milestoneId)` MUST emit `MilestoneReached` when criteria pass and `MilestoneSlipped` otherwise; plans/milestones MUST be version-aware (append).
- **FR-008**: `MilestoneReached` criteria MUST be explicit: all linked work items in `Completed` (or per-criteria status) plus required evidence approved; criteria are part of the explanation, not implicit.
- **FR-009**: System MUST provide read models: `GetProjectHealth(projectId)` (completion %, overdue/atRisk/blocked counts, upcoming deadlines, metric violations, milestone status) and `GetManagerDashboard(managerId)` (totals, active, overdue, blocked, tasksBySubordinate, completion %, critical, upcoming deadlines, projectHealth, metric violations) — every aggregation MUST be subtree/membership-filtered via `IManagementHierarchy` (Golden Rule A) **before** fetching, never post-filtered, and tenant-aware.
- **FR-010**: When `IMetricEvaluationPolicy` evaluates metric values against thresholds, violation MUST emit `MetricThresholdViolated` (in `MetricValue` aggregate) and the violation MUST appear in both `GetProjectHealth` and `GetManagerDashboard` for viewers with visibility; no violation MUST be silently dropped.
- **FR-011**: Historical reconstructibility: `ExplainProgress(workItemId, asOf?: DateTime)` MUST return the persisted explanation active at that time (versioned), enabling reconstruction without recomputing from live state; explanations MUST be append-only and queryable by `(workItemId, computedAt)`.
- **FR-012**: System MUST expose commands `DefineMetric`, `UpdateMetricDefinition`, `OverrideProgressManually`, `CreateMilestone`, `EvaluateMilestone` with `Validator` + `Handler` + `IEndpoint` + `Result<Error>` + transactional outbox (per BuildingBlocks `ISender`, `IPipelineBehavior`, `IOutboxWriter`), and queries above with stable `Result` contracts (never leaking domain entities).
- **FR-013**: Triggering: progress recalculation MUST be triggerable both on demand and automatically when SPEC-003 domain events occur (`WorkItemStatusChanged`, `WorkItemCompleted`, `ProgressRecalculated` ancestor, evidence approved) via EventBus integration; handlers MUST be idempotent and strategy-deterministic.
- **FR-014**: Versioning and concurrency: metric definitions, milestones, and explanations MUST be append-only/versioned; current definitions MUST use optimistic concurrency where mutable in place (via `RowVersion`); history MUST never be overwritten.

### Key Entities

- **MetricDefinition** (AggregateRoot, `MetricDefinitionId` StronglyTypedId): Code, Name, `MetricDimension` Enumeration, `MetricWeight` VO (0–1), `MetricTarget` VO (0–100), `MetricThreshold` VO, `requiresEvidence` bool, `TenantId`, `ProjectId|TemplateId`, `Version`, `EffectiveFrom`; seeded dimensions extensible. Events: `MetricDefinitionCreated`, `MetricDefinitionUpdated`.
- **MetricValue** (AggregateRoot, `MetricValueId`): Evaluated value per metric/instant; fields `DefinitionId`, `ProjectId`, `Value`, `Threshold`, `IsViolated`; event `MetricThresholdViolated` when `value < threshold` (or > depending on dimension).
- **Milestone** (AggregateRoot, `MilestoneId`): `ProjectId`, `Title`, `DueDate`, `Criteria` (linked work item IDs + evidence IDs), `Status` (Planned/Reached/Slipped), `Version`, `TenantId`; events `MilestoneReached`, `MilestoneSlipped`; plan version-aware.
- **ProgressExplanation** (VO or append Entity — stored per computation): `WorkItemId`, `ProjectId`, `StrategyId`, `ComputedAt`, `ResultPercent`, `WeightsSum`, `Components[{name, weight, progress, contribution, isOverride}]`, `InputsSnapshot` (subtask statuses snapshot + evidence list), reconstructible formula `Σ(w×p)/Σw`.
- **DeadlineStatus** (Enumeration/VO): `OnTime`, `AtRisk` (within window), `Overdue` (due < today & incomplete), `CompletedOnTime`, `CompletedLate` — derived by `IDeadlineEvaluator`, not UI.
- **MetricDimension / MetricWeight / MetricTarget / MetricThreshold / ComponentValue** (VOs): Validated at construction; weight normalized sum aware; violation logic owned by `IMetricEvaluationPolicy`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Given identical subtask/evidence/milestone inputs and strategy, when `ExplainProgress` is invoked twice without state change, it returns byte-identical `ProgressExplanation` (same `resultPercent`, `weightsSum`, `components` with contributions, same snapshot) — verified by computing twice and asserting equality.
- **SC-002**: Given a parent with 4 weighted subtasks (weights 1,1,1,2 where 3 of 4 with 3×1 completed at 100% and the weighted one at 0%), when `ExplainProgress` runs, it returns `60%` with 4 components showing arithmetic `(1+1+1+0)/5` and strategy ID `weightedSubtask`.
- **SC-003**: Given a metric with threshold 80% violated (e.g., completion 62%), when `IMetricEvaluationPolicy` evaluates, `MetricThresholdViolated` is emitted and visible in `GetManagerDashboard(managerSubtreeVisible)` and `GetProjectHealth` for that project — verified by threshold-violated fixture then two queries.
- **SC-004**: Given a manual override with justification `OverrideProgressManually(workItemId, 90%, "demo", actor)`, when committed, then an audit entry `audit.progress.overridden` with actor, previous, new, justification is persisted via same-tx outbox, and the next `ExplainProgress` shows `isOverride=true` with the override component and justification.
- **SC-005**: Given two managers with disjoint subtrees/memberships, when each calls `GetManagerDashboard(managerId)`, then each dashboard aggregates only its Golden Rule A-visible projects (subtree via `IManagementHierarchy` before fetch), never the other's, and tenant isolation holds — verified by subtree-filtered fixture with two managers.
- **SC-006**: Given a historical date, when `ExplainProgress(workItemId, asOf=2026-08-01)` is called, then the returned `ProgressExplanation` equals the explanation version that was active at that date (not the latest), enabling reconstruction without recomputing.
- **SC-007**: Deadline transitions are correct: task due tomorrow `InProgress` → `OnTime`, due in 2 days with 3-day atRisk window → `AtRisk`, due yesterday incomplete → `Overdue`, completed before due → `CompletedOnTime`, completed after due → `CompletedLate` — verified by date-boundary unit suite.
- **SC-008**: Progress is never an unexplained number: every computed percentage has a persisted `ProgressExplanation` with strategy, weightsSum, components, and snapshot reachable via `ExplainProgress`; absence of explanation is a failure.

## Assumptions

- `IManagementHierarchy.IsInSubtree/GetSubtree` and `IAuthorizationEvaluator` (permission `progress.override`, `metric.define`) from SPEC-002 are available as Shared Kernel and consumed before any metric/override write; until ready, tests use in-memory stubs per BuildingBlocks `Specification<T>` patterns.
- `OroIdentityServer` OIDC identity and `tenant_id` as `TenantContext` (SPEC-002) are configured; every command/query carries actor+tenant.
- Default `atRiskWindowDays = 3` per project (configurable per project via metric/project settings); deadline evaluation uses UTC midnight truncation, not local time.
- Strategies shipped: `WeightedSubtaskStrategy` (default) and `DeliverableMilestoneStrategy`; adding a new strategy is `IProgressCalculationStrategy` registration (no aggregate change); selection stored per project as `strategyId`.
- Zero-weight handling: if `Σweight == 0` or no components, result is `0%` with `zeroWeight=true` in explanation, never division-by-zero.
- Metric dimensions seeded with the 10 listed; adding a dimension is an enumeration seed row; metric templates are cloneable per project.
- Milestone criteria evaluation is explicit: default is `all linked WorkItems status == Completed` plus `requiresEvidence=> evidence approved`; richer verification (e.g., quality gate) is a future policy hook.
- Progress recalculation subscriptions to SPEC-003 events (`WorkItemStatusChanged`, `WorkItemCompleted`, etc.) are via RabbitMQ topic `workitem.*` with idempotent handlers; on-demand recalculation via `POST /progress/{workItemId}/recalculate`.
- Versioning: `MetricDefinition` and `Milestone` use append version rows; `ProgressExplanation` is append-only per computation (`workItemId, computedAt`); optimistic concurrency via `RowVersion` on current mutable rows where in-place edits exist.
- Notifications for `MetricThresholdViolated`/`MilestoneReached` are published as integration events consumed by SPEC-008; this spec only emits via outbox.

## Dependencies & Traceability

- **Depends on**: SPEC-003 Projects & Work Management — `WorkItem`, `WorkItemType`, `ProgressExplanation` placeholder, `WorkItemStatusChanged`/`Completed` events, `Milestone` entity; SPEC-002 Identity — `IManagementHierarchy`, `IAuthorizationEvaluator`, `TenantContext`, audit outbox pattern.
- **Enables**: SPEC-008 Notifications (consumes `MetricThresholdViolated`, `MilestoneReached`); future analytics modules.
- **Constitution**: Principles XII (explainable progress), XIII (configurable metrics), VI (rules in domain via `IBusinessRule`), VII/VIII (hierarchical auth + auditable), XV (tenant-aware), XVI (APIs are contracts), V (modular BC-04), XX/XXI (testability + TDD+DDD+Vertical Slices), XXII (skills govern). §Versioning, §Work Item Model.

## Out of Scope

- Full document lifecycle linked to metrics beyond evidence ID reference (BC-06 Documents).
- AI/LLM-derived metrics beyond manual/evidence inputs (BC-08 AI/LLM Processing).
- Search/indexing across metrics beyond authorization-filtered queries (BC-07 Search).
- Real-time push/WebSocket for dashboard updates — poll/query via `IManagementHierarchy`-filtered read models only.
