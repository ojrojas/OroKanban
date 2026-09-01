# Contract: Metrics API

**Module**: `Metrics` (BC-04) | **Base path**: `/api/metrics` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP` 400/403/409, tenant-aware, `IEndpoint` per slice.

## POST /api/metrics/definitions — DefineMetric

**Command**: `DefineMetricCommand : ICommand<Result<MetricDefinitionResponse>>`

```json
// Request
{
  "projectId": "guid | null (null=template)",
  "templateId": "guid | null",
  "code": "delivery-date",
  "name": "Delivery Date Adherence",
  "dimension": "DeadlineAdherence",
  "weight": 0.3,
  "target": 100,
  "threshold": 80,
  "requiresEvidence": false
}
// Response 201 — Location: /api/metrics/definitions/{id}
{
  "id": "guid (MetricDefinitionId)",
  "projectId": "guid | null",
  "templateId": "guid | null",
  "code": "delivery-date",
  "name": "Delivery Date Adherence",
  "dimension": "DeadlineAdherence",
  "weight": 0.3,
  "target": 100,
  "threshold": 80,
  "requiresEvidence": false,
  "version": 1,
  "isCurrent": true,
  "effectiveFrom": "2026-09-01T...",
  "tenantId": "guid"
}
// Errors: 400 Validation (duplicate code in project/template, weight 0–1, target/threshold 0–100, unknown dimension → Dimension.Enumeration not found), 403 Forbidden (metric.define via IAuthorizationEvaluator), 404 project/template not found
```

**Domain**: `MetricDefinition.Create(...)` → `CheckRule(WeightValidRule)` + `Dimension.Exists` → `MetricDefinitionCreated` → outbox `MetricDefinitionCreatedIntegrationEvent`. Version 1. `RowVersion` for current row.

## PUT /api/metrics/definitions/{id} — UpdateMetricDefinition

**Command**: `UpdateMetricDefinitionCommand(id, weight?, target?, threshold?, name?, requiresEvidence?) : ICommand<Result<MetricDefinitionResponse>>`

Updates create **new version row** (append): `version = maxVersion+1`, `EffectiveFrom=UtcNow`, `IsCurrent=true` (previous `IsCurrent=false`). Returns new version.

```json
// Request (partial)
{ "weight": 0.5, "expectedVersion": 1 } // optimistic concurrency on current row
// Response 200 — new version
{ "id": "guid (same logical id, new row id distinct)", "version": 2, "isCurrent": true, "weight": 0.5 }
// Errors: 409 Conflict (Stale RowVersion / expectedVersion mismatch), 400 validation, 403 forbidden
```

## GET /api/metrics/definitions?projectId=&includeHistory= — List + History

```
GET /api/metrics/definitions?projectId={guid}&includeHistory=false → current only
GET /api/metrics/definitions?projectId={guid}&code=delivery-date&asOf=2026-08-01 → version active at date (EffectiveFrom <= asOf ORDER BY version DESC LIMIT 1)
```

## POST /api/metrics/definitions/clone — CloneTemplate

**Command**: `CloneMetricTemplateCommand(templateId, targetProjectId)` → copies all definitions from template as version 1 rows for target project (diverge independently). Returns count.

## GET /api/metrics/values?projectId=&definitionId= — Metric Values (threshold evaluation)

Queried via `GetProjectHealth`/`GetManagerDashboard`; direct `GET /api/metrics/values` is paginated read (`MetricValueResponse {definitionId, value, threshold, isViolated, computedAt}`) already filtered before fetch.
