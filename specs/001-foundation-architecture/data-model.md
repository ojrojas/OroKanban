# Data Model: Foundation and Architecture

**Feature**: 002-foundation-architecture | **Date**: 2026-08-31

Platform context holds no business aggregates. Its "model" is the composition itself — modules, their DbContexts, cross-module contracts, and the external identity resource. The entities below describe that composition so that contracts and tests have stable anchors.

## Entities

### 1. Module

A bounded context as a physical module (`src/Modules/<Name>/`). — Platform entity, not a runtime aggregate.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `name` | `string` | `Identity`, `Organization`, `Projects`, `Metrics`, `Documents`, `AiProcessing`, `Search`, `Audit`, `Notifications` | Maps to BC-01..BC-09 per constitution Principle V |
| `solutionFolder` | `string` | `/src/Modules/<Name>` in `OroKanban.slnx` | Folder per discovery §3 convention |
| `projects` | `ModuleProject[4]` | Exactly `Domain`, `Application`, `Infrastructure`, `Contracts` | Created via `dotnet new classlib` per FR-010 |
| `domainProjectRef` | `ProjectReference?` | MAY be referenced by its own Application/Infrastructure | No cross-module Domain/Infrastructure refs (architecture test) |
| `contractsProjectRef` | `ProjectReference?` | MAY be referenced by other modules | Only Contracts cross the boundary |

**Relationships**: `Module` 1 — 1 `ModuleDbContext` (Infrastructure); `Module` 1 — * `IntegrationEvent` via `Contracts`; `Module` referenced by `OroKanban.AppHost` as a service resource.

**Validation**: All 9 modules exist; `BuildingBlocks` stays untouched; no module references another module's Domain/Infrastructure — guard by `tests/Architecture`.

### 2. ModuleDbContext

Per-module EF Core context. Inherits `BuildingBlocks.Kernel.Domain` `AppDbContextBase`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `type` | `class` | `: AppDbContextBase`, non-abstract, in `*.Infrastructure` | Architecture test (c) |
| `provider` | `enum` | `Npgsql` exclusively | `Npgsql.EntityFrameworkCore.PostgreSQL` pin in `Directory.Packages.props` |
| `schema` | `string` | Per-module schema, e.g., `identity`, `projects` | `HasDefaultSchema` in `OnModelCreating`; prevents table collision |
| `outboxConfig` | `IEntityTypeConfiguration` | `ApplyConfiguration(new OutboxEntityTypeConfiguration())` | Transactional outbox per BuildingBlocks canon |
| `concurrency` | `byte[] RowVersion` / `uint xmin` | On base aggregate (`AggregateRoot<TId>`) | Optimistic concurrency — stale save → concurrency Error |
| `connectionString` | `string` | From Aspire `WithReference(postgres)` + `WaitFor` | Per-environment, never hard-coded |

**State transitions**: `Created` (scaffold) → `Configured` (AppDbContextBase+outbox+Npgsql+schema) → `Migrated` (first `dotnet ef migrations add` migration applied).

### 3. IntegrationEvent (Contract)

Cross-module event inheriting `BuildingBlocks.EventBus` `IntegrationEvent`. Lives in `Contracts`.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `eventType` | `string` | PascalCase, e.g., `OrganizationHierarchyChangedIntegrationEvent` | Topic routing key for RabbitMQ |
| `payloadDto` | `record` | Public DTO, no domain internals | Stable contract per Principle XVI |
| `producerModule` | `Module` | Single producer | Emitted via outbox in producer's transaction |
| `consumerModules` | `Module[]` | Zero or more | Each consumer has its own queue (at-least-once) |
| `routingKey` | `string` | `orokanban.<bounded-context>.<event>` | Topic exchange convention |

**Invariants**: Consumers are idempotent; payload contains `TenantId`/`CorrelationId` for audit tracing.

### 4. ExternalIdentityResource

The `oroidentityserver` Podman container consumed, not implemented.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `discoveryEndpoint` | `Uri` | `GET {authority}/.well-known/openid-configuration` | OpenIddict metadata |
| `authority` | `string` | Env-only: `Identity__Authority` | No default; fail-fast if absent |
| `clientId` / `clientSecret` | `string` | Env-only: `Identity__ClientId` / `Identity__ClientSecret` | Registered via `POST /api/applications` (`authorization_code` + `refresh_token`) |
| `audience` / `scopes` | `string` | Env-only, e.g., `orokanban-api` | JWT validation parameters |
| `tenantClaim` | `string` | `tenant_id` from `/connect/userinfo` | Propagated as tenant context via `IClaimsTransformation` or middleware |
| `containerResource` | `Aspire IResourceBuilder` | `AddContainer("oroidentityserver", "ghcr.io/ojrojas/oroidentityserver", ...)` | Declared in AppHost, dashboard-visible |

**Invariants**: Never duplicated; always external; missing config → fail-closed (401/ startup error per Edge Cases).

### 5. SeedDevelopmentData (Command)

Dev-only vertical slice in `src/Api/Features/SeedDevelopmentData`.

| Field | Type | Constraints |
|-------|------|-------------|
| `organizationName` | `string` | e.g., `OroKanban Demo Org` |
| `adminEmail` | `string` | Valid email |
| `isDevOnly` | `bool` | `true` — guarded by `if (env.IsDevelopment())` / feature flag |

**Behavior**: Calls OroIdentityServer admin APIs (`POST /api/tenants`, `/api/users`, `/api/users/{id}/roles`) to bootstrap tenant/org, users, roles. No credential persistence in OroKanban; returns `Result`.

### 6. PlatformHealth (Query Result)

Result of `GetPlatformHealth` query in `src/Api/Features/GetPlatformHealth`.

| Field | Type | Constraints |
|-------|------|-------------|
| `modules` | `ModuleHealth[]` | Each: `name`, `status` (`Healthy`/`Degraded`/`Unhealthy`), `dbReachable`, `outboxBacklog` |
| `identity` | `IdentityHealth` | `reachable` (`true`/`false`), `discoveryEndpoint`, `latencyMs`, `error` (if unreachable) |
| `infra` | `InfraHealth` | `postgres`, `rabbitmq`, `redis` each with `status` + `endpoint` |

**Behavior**: Aggregates `HealthCheck` results (from `AddServiceDefaults()` probes) plus a live fetch of `/.well-known/openid-configuration` for identity reachability; never crashes the caller when identity is down (reports `Unhealthy` with reason).

## Relationships Overview

```
Module (9) ─1── ModuleDbContext ──uses── Npgsql / Outbox / RowVersion
   │ produces/consumes
   └── IntegrationEvent (Contracts, topic-routed via RabbitMQ)
AppHost ─composes── Module services + Api + Web + Postgres + RabbitMQ + Redis + ExternalIdentityResource
Api ─exposes── SeedDevelopmentData (command, dev-only) + GetPlatformHealth (query) + /health /alive
ExternalIdentityResource ─validates── Api JWT bearer (Authority + tenant_id propagation)
```

## Validation Rules (from spec)

- Every Module has exactly 4 projects (FR-002); each DbContext is `AppDbContextBase`+outbox+Npgsql+rowVersion (FR-003).
- AppHost declares Postgres, RabbitMQ, Redis, external oroidentityserver, Api, Web (FR-004).
- Identity is env-only, multi-tenant via `tenant_id` (FR-005).
- All services call `AddServiceDefaults()` + Serilog (FR-006).
- `tests/Architecture` guards MediatR/MassTransit/AutoMapper + cross-module boundaries + DbContext inheritance (FR-007).
- Scaffolding recorded (`ng new` / `dotnet new webapi` / `dotnet new classlib`) per FR-010.
