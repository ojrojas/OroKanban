# Data Model: Metrics, Progress and Planning

**Feature**: 004-metrics-progress-planning | **Date**: 2026-09-01 | **Schema**: `metrics` (`MetricsDbContext : AppDbContextBase`, Npgsql, `HasDefaultSchema("metrics")` + `OutboxEntityTypeConfiguration()`)

## Entities

### 1. MetricDefinition (AggregateRoot, BC-04, `metrics.metric_definitions`)

Per project or per template (template = `ProjectId` null + `TemplateId`)? For 004, per-project first; template clone via copy.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `MetricDefinitionId : StronglyTypedId<Guid>` | PK | Root |
| `ProjectId` | `Guid?` | nullable — null means template | Per project or per template; unique with `Code+Version` |
| `TemplateId` | `Guid?` | nullable | Alternative to ProjectId; exactly one not null |
| `Code` | `string` | required, 1–50, `^[a-z0-9_-]+$`, trimmed/lowercased, unique per `ProjectId+Code+Version` (or `TemplateId`) | Business key |
| `Name` | `string` | required, 3–100 | Display |
| `DimensionId` | `int` | FK `MetricDimension` Enumeration | Completion, DeadlineAdherence, etc. (10 seeded) |
| `Weight` | `MetricWeight : ValueObject` | `0–1`, 2 decimals, validated | Normalized contribution; `0` allowed but zeroWeight path |
| `Target` | `MetricTarget : ValueObject` | `0–100` | Desired value |
| `Threshold` | `MetricThreshold : ValueObject` | `0–100` | Violation point |
| `RequiresEvidence` | `bool` |  | If true, needs approved evidence |
| `Version` | `int` | starts 1, `Version+1` on update, append row; `IsCurrent` bool | Append-only |
| `EffectiveFrom` | `DateTime` | UTC `UtcNow` at create | For historical `asOf` |
| `IsCurrent` | `bool` |  | Denotes latest version |
| `TenantId` | `Guid` | required | Tenant isolation |
| `RowVersion` | `byte[]` | `IsRowVersion()` | Concurrency on current row only |

**Events**: `MetricDefinitionCreated {MetricDefinitionId, ProjectId, Dimension, Version}`, `MetricDefinitionUpdated {MetricDefinitionId, Version, ProjectId}`.

**History query**: `WHERE ProjectId==pid AND Code==code ORDER BY Version DESC` or `WHERE EffectiveFrom <= asOf ORDER BY Version DESC LIMIT 1`.

### 2. Milestone (AggregateRoot, BC-04, `metrics.milestones` + join `metrics.milestone_work_items`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `MilestoneId : StronglyTypedId<Guid>` | PK | |
| `ProjectId` | `Guid` | FK to Projects project, required | Verifiable within project |
| `Title` | `string` | required, 3–100 | |
| `Description` | `string?` | max 1k | |
| `DueDate` | `DateTime` | required, UTC | Dated |
| `Status` | `int` | `Planned(1)/Reached(2)/Slipped(3)` | Derived via evaluation |
| `LinkedWorkItemIds` | `Guid[]` | join table `milestone_work_items` (`MilestoneId`, `WorkItemId`), cross-project validation rejects unless policy allows | Evidence predicate |
| `Criteria` | `string?` | serialized `MilestoneCriteria` VO (e.g., `allCompleted`, `atLeast 2/3`) | Verifiable |
| `Version` | `int` | append on update, `IsCurrent` | Plan version-aware |
| `EffectiveFrom` | `DateTime` |  | Historical |
| `IsCurrent` | `bool` |  | |
| `TenantId` | `Guid` | required | |
| `RowVersion` | `byte[]` | `IsRowVersion()` | |

**Events**: `MilestoneCreated`, `MilestoneReached {MilestoneId, ProjectId, ReachedAt}`, `MilestoneSlipped {MilestoneId, RemainingWorkItemIds}`.

### 3. MetricValue (AggregateRoot or append per evaluation, `metrics.metric_values`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `MetricValueId : StronglyTypedId<Guid>` | PK | |
| `DefinitionId` | `MetricDefinitionId` | FK `MetricDefinitionId` | |
| `ProjectId` | `Guid` |  | Denormalized for fast dashboard filter |
| `Value` | `decimal` | 0–100 | Computed metric value (e.g., completion %) |
| `Threshold` | `decimal` | 0–100 | Copied from definition at evaluation time (versioned) |
| `IsViolated` | `bool` | computed `Value < Threshold` (or > depending dimension polarity) | Drives dashboard |
| `ComputedAt` | `DateTime` | UTC | Append timestamp |
| `TenantId` | `Guid` | required | |

**Event**: `MetricThresholdViolated {MetricValueId, DefinitionId, Value, Threshold}` — emitted when `IsViolated` transitions true; both `GetProjectHealth` and `GetManagerDashboard` project this.

### 4. ProgressExplanation (append entity, `metrics.progress_explanations`)

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `Id` | `Guid` | PK | Append per computation |
| `WorkItemId` | `Guid` | required, indexed + `ProjectId` | Source |
| `ProjectId` | `Guid` |  | Denormalized |
| `StrategyId` | `string` | e.g., `weightedSubtask`, `deliverableMilestone` | Reproducibility |
| `ComputedAt` | `DateTime` | required, indexed descending | Historical `asOf` key |
| `ResultPercent` | `decimal` | 0–100 | `Σ(w×p)/Σw` |
| `WeightsSum` | `decimal` |  | Denominator |
| `ZeroWeight` | `bool` |  | True when `Σw==0` |
| `IsOverride` | `bool` |  | Manual override flag |
| `OverrideJustification` | `string?` | max 500 | Audited justification when `isOverride` |
| `OverrideActorId` | `Guid?` |  | Audited actor |
| `ComponentsJson` | `string` | `jsonb` `ComponentValue[{name, weight, progress, contribution, isOverride}]` | Deterministic ordering |
| `InputsSnapshotJson` | `string` | `jsonb` snapshot of `SubtaskSnapshot[{workItemId, progress, completed}] + EvidenceIds + MilestoneIds` at compute time | Reproducibility |
| `TenantId` | `Guid` | required | |
| `RowVersion` | `byte[]` | (on latest if mutable, but history is append) | |

**Indexes**: `IX_progress_explanations_workItem_computedAt (WorkItemId, ComputedAt DESC)`, `IX_project_computedAt`.

**Determinism**: Same `InputsSnapshot` + `strategyId` → identical `ComponentsJson` + `ResultPercent` (validated by byte-identical test).

### 5. VOs & Enumerations

| VO | Fields | Validation | Notes |
|----|--------|------------|-------|
| `MetricDimension` | `Id int, Name string` | Enumeration `Completion(1), DeadlineAdherence(2), ContentCompleteness(3), Quality(4), Risk(5), Criticality(6), Effort(7), DependencyHealth(8), DocumentCompliance(9), ReviewStatus(10)` — seed rows, extensible | Dimension polarity (violation direction) lives in policy |
| `MetricWeight` | `Value decimal 0–1` | `0<=v<=1`, 2 decimals | Normalized |
| `MetricTarget` | `Value decimal 0–100` |  | Desired |
| `MetricThreshold` | `Value decimal 0–100` |  | Violation point |
| `DeadlineStatus` | `Enumeration` `OnTime(1), AtRisk(2), Overdue(3), CompletedOnTime(4), CompletedLate(5)` | Derived pure, not UI string | Via `IDeadlineEvaluator` |
| `ComponentValue` | `Name string, Weight decimal, Progress decimal 0–100, Contribution decimal` | `Contribution = progress*weight` | Inside `ProgressExplanation.ComponentsJson` |
| `MilestoneCriteria` | `RequiredStatus int, LinkedWorkItemIds Guid[]` |  | Verifiable predicate |

### 6. Domain Services (injectable, pure/testable)

| Service | Contract | Published By | Implemented By |
|---------|----------|--------------|----------------|
| `IProgressCalculationStrategy` | `ProgressExplanation Calculate(ProgressInputs inputs)` | `Metrics.Domain` | `Metrics.Infrastructure/Strategies/WeightedSubtaskStrategy`, `DeliverableMilestoneStrategy` (factory `IStrategyResolver` maps `project.strategyId`) |
| `IMetricEvaluationPolicy` | `MetricValue Evaluate(MetricDefinition def, decimal actualValue)` → emits violation | `Metrics.Domain` | `Metrics.Infrastructure` |
| `IDeadlineEvaluator` | `DeadlineStatus Evaluate(DateTime? dueDate, int statusId, DateTime completedAt?, int atRiskWindowDays, DateTime nowUtc)` | `Metrics.Domain` | `Metrics.Infrastructure` pure |
| `IWorkItemSnapshotProvider` | `Task<IReadOnlyList<SubtaskSnapshot>> GetSubtasksAsync(workItemId, ct)` | `Metrics.Contracts` | `Metrics.Infrastructure` (reads `ProjectsDbContext` read-only) |
| `IManagementHierarchy` | `GetSubtreeAsync` etc. | `Organization.Contracts` (consumed) | `Organization.Infrastructure` |

### Relationships Overview

```
MetricDefinition (1) ──< MetricDefinition version rows (append, code+project+version unique)
        │ 1
        └────────── MetricValue (many, DefinitionId)
                       └─ embraces MetricThresholdViolated event

Milestone (1) ──< MilestoneWorkItem (join, many workItemIds)
        │ versioned append (Plan)

WorkItem (Projects, 1) ──< ProgressExplanation (append many, WorkItemId+ComputedAt)
        │ (via IWorkItemSnapshotProvider read of child WorkItems)
        └─ IProgressCalculationStrategy → Σ(w×p)/Σw → ProgressExplanation (deterministic)

DeadlineStatus ← IDeadlineEvaluator (pure, atRiskWindowDays per ProjectSettings)

All read models (GetProjectHealth/GetManagerDashboard) filter ProjectId via IManagementHierarchy subtree before aggregation → TenantId first.
```
