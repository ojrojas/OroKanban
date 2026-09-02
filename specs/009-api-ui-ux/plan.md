# Implementation Plan: API, UI and User Experience

**Branch**: `009-api-ui-ux` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md) | **Depends on**: 002-identity-organization … 008-notifications

**Input**: SPEC-009 — BC-10 Platform + all read models. R1 API contracts first (pagination/filter/sort/search, optimistic concurrency ETag/version, ProblemDetails via `Result→HTTP`), R2 12 views minimum, R3 manager dashboard subtree-filtered, R4 work item detail (progress with explanation link), R5 `minimal-ui-design-system` tokens/elevation/components, R6 `ngrx-signal-store` state, R7 UI hides/API enforces. Flujo de uso constitution: `Foundation → Identity → Organization hierarchy → Projects → Work items → Metrics/progress → Documents → Search/indexing → LLM processing → Audit/monitoring → Notifications → Administration`.

## Summary

Implementar BC-10 Platform como capa horizontal que estabiliza contratos API (DTOs + envelope `Paged<T>` + `ProblemDetails` + `ETag`/`version` 409/412) y construye el shell Web Angular con navegación role/branch-aware, 12+ páginas planificadas y controles reutilizables gobernados estrictamente por `minimal-ui-design-system` (tokens `colors/typography/spacing/radius` + sistema de ELEVACIÓN `flat` vs `shadow-elevated` + patrones `nav/top bar/KPI cards/lists/badges`) y estado frontend exclusivamente en `NgRx SignalStore` (`signalStore/withState/withComputed/withMethods/withProps/withEntities/withHooks/rxjs-interop`). El orden de navegación sigue la constitution y cada vista resuelve su read model vía `Api` (`IEndpoint` + `Result→HTTP` + `Specification<T>` + `IManagementHierarchy` subtree) — la UI oculta lo no autorizado, el API deniega (XIX). Todo se diseña primero en `research/tokens/components/layout` y luego se implementa en stores/páginas testeables.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 `global.json`) para `Api` + TypeScript 5.6 / Angular 22.1 para `Web` (`@ngrx/signals` 22, `rxjs` 7.8, `angular-auth-oidc-client` 17) — ya en `src/Web/package.json`.

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (`AggregateRoot`, `StronglyTypedId`, `Enumeration`, `ValueObject`, `IBusinessRule`, `Specification<T>`, `Result/Error`, `IRepository`), `BuildingBlocks.CQRS` (`ISender`, `ICommand/IQuery`, `IValidator`, `IPipelineBehavior` Validation/Logging), `BuildingBlocks.ServiceDefaults` (`AddServiceDefaults` OTel/Serilog/health, `IEndpoint`, `ResultExtensions.ToHttpResult/ToProblem`, `GlobalExceptionHandler`), `BuildingBlocks.EventBus` (solo para read models que consumen eventos), `Microsoft.AspNetCore.Authentication.JwtBearer` + `angular-auth-oidc-client` (OIDC contra `oroidentityserver` Podman externo), `Npgsql.EntityFrameworkCore.PostgreSQL` (solo para read models que lo requieren; la mayoría son proyecciones vía queries filtradas). **Frontend:** `@ngrx/signals` (`signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, `withEntities`), `@ngrx/signals/rxjs-interop` (`rxMethod`), `@ngrx/operators`, `angular-auth-oidc-client` para `access_token`. **Design:** `.agents/skills/minimal-ui-design-system` (`references/tokens.md` + `components.md` + `layout.md`).

**Storage**: Postgres `orokanban` (via Aspire) para read models persistidos (`projects`, `documents`, `audit`, etc.) — ya existente. Web **sin storage local persistente** más allá de `localStorage` para `tenant_id`/`theme`; el resto es `SignalStore` en memoria + `Api` como fuente de verdad. No `IndexedDB`.

**Testing**: `dotnet`: xUnit + `TestHost` para contratos (`ProblemDetails`, envelope, `ETag` 409/412), integración para filtros subtree. `Web`: Vitest 4 + JSDOM (`npm test` en `src/Web` con `ng test` → Vitest) + `ngrx/signals` testing (`provideMockStore`-style, `patchState`/`getState`), Playwright para E2E `role→nav` y `Kanban→detail→concurrency`. `BuildingBlocks.Logger` + OTel para observabilidad (Principle XVIII).

**Target Platform**: Web SPA Angular servida por `OroKanban.AppHost` (`Aspire` `AddProject("api")` + `AddNpmApp("web")` / `AddDockerfile("web")` en publish), Navegador moderno + Linux containers. `oroidentityserver` externo Podman `localhost/oroidentityserver:latest` en `5080/5086`.

**Project Type**: Modular monolith + SPA (Web application): `src/Api` (vertical slices `IEndpoint`) + `src/Web` (Angular standalone, lazy routes por vista) sobre `src/Modules/*` ya existentes.

**Performance Goals**: Listas 100 items `pageSize 20` <300ms p95; `Dashboard` 10 KPIs subtree <500ms; `Kanban` drag-drop + `PUT` con `ETag` <400ms; cambio de ruta <100ms; `SignalStore` `switchMap` cancela previos sin fuga; build `ng build` <60s.

**Constraints**: Principio I/XXII: reutilizar `BuildingBlocks` + skills `minimal-ui-design-system` y `ngrx-signal-store` como bases canónicas — ningún color/shadow/spacing fuera de tokens, ningún `BehaviorSubject` para estado de feature. Principio II: solo `oroidentityserver` vía OIDC, nunca SQL a `identitydb`. Principio XVI: DTOs estables, nunca entidades internas. Principio XIX: UI oculta, API deniega — hidings no son seguridad. Principio VII/XV: filtros subtree/tenant siempre antes del fetch.

**Scale/Scope**: 12 vistas mínimas + 4 auxiliares (Login, Organization hierarchy, Search, Planning detail) = ~16 rutas; ~12 `SignalStore`s (`dashboard`, `projects`, `kanban`, `workItemDetail`, `myTasks`, `teamTasks`, `planning`, `documents`, `aiQueue`, `notifications`, `audit`, `admin`) + ~15 componentes reutilizables (nav, topBar, kpiCard, listCard, badge, button, input, modal, pagination, filterPill, searchBar, avatarRow, chartCard, timeline, auditRow) — todos `flat` o `shadow-elevated` según `tokens.md`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reusa `BuildingBlocks` canon (`Entity`, `AggregateRoot`, `ValueObject`, `StronglyTypedId`, `Enumeration`, `Specification<T>`, `Result/Error`, `ISender`, `IEndpoint/Result→HTTP`, `AppDbContextBase`, `EfRepository`, `Outbox`, `ServiceDefaults`) + `draft/oroidentityserver-specification.md` + skills `.agents/skills/minimal-ui-design-system` y `ngrx-signal-store` como bases obligatorias para arquitectura/UI/estado. No introduce `MediatR`/`MassTransit`/`AutoMapper`.
- [x] **II — oroidentityserver Mandatory**: Solo integración OIDC vía `angular-auth-oidc-client` + `Api` `JwtBearer` validando `oroidentityserver` Podman; nunca `DbContext` a `identitydb` (ya eliminado `IdentityDbContext`). Claims `sub`/`tenant_id`/`roles`.
- [x] **III — .NET 10**: `global.json` 10.0.400, `Directory.Packages.props` central.
- [x] **IV — Aspire Orchestrator**: `OroKanban.AppHost/AppHost.cs:19` `AddContainer("identity-api")` + `AddProject("api")` + `AddNpmApp("web")`; no duplicación de infraestructura.
- [x] **V — Modular Architecture**: `BC-10 Platform` expone contratos/lecturas; cada módulo expone `Contracts` vía `IEndpoint`; sin acceso cruzado a DB interna — solo `ISender`/`Specification`/`IEventBus`.
- [x] **VI — Domain Rules Belong to Domain**: Validaciones y transiciones de estado (`DocumentStatus`, `WorkItemStatus`) permanecen en `Domain` (`IBusinessRule`/`Specification`), UI solo refleja.
- [x] **VII — Hierarchical Authorization (NON-NEGOTIABLE)**: Toda query lista/dashboard compone `IManagementHierarchy` subtree ilimitado antes del fetch; tests de límites `Contributor` vs `Manager` + profundidad 5 niveles.
- [x] **VIII — Everything Important Is Auditable**: Acciones sensibles via `outbox` → `AuditEntry` append-only; UI solo consume.
- [x] **XVI — APIs Are Contracts (NON-NEGOTIABLE)**: DTOs estables, envelope `Paged<T>` `{items,total,page,pageSize,Link}`, `ProblemDetails` via `Result→HTTP`, `ETag`/`version` 409/412, nunca entidades.
- [x] **XV — Data Must Be Tenant/Organization Aware**: Todo `Specification<T>` incluye `tenant_id` + `organizationId` en subtree; UI jamás bypass.
- [x] **XIX — Security by Default**: deny-by-default, least privilege, UI solo oculta, API autoridad, `ProblemDetails` sin leak, secrets via Aspire.
- [x] **XXII — Workspace Skills Govern Architecture & Resource Design**: `minimal-ui-design-system` (tokens/elevación/componentes) y `ngrx-signal-store` (signalStore/withState/withComputed/withMethods/withProps/withEntities/withHooks/rxjs-interop) son obligatorios; desviación requiere ADR.
- [x] **XX — Testability**: Contrato + E2E role/nav + store unit per skill.
- [x] **XXI — TDD + DDD + Vertical Slices**: `IEndpoint` por slice, `Result`/`Error`, tests primero.

**Result: PASS — no violations, no complexity exceptions.** Re-check after Phase 1 expected PASS.

## Project Structure

### Documentation (this feature)

```text
specs/009-api-ui-ux/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── api-contracts.md              # Pagination envelope, ProblemDetails, ETag/version, filter/sort/search
│   ├── navigation-and-access.md      # Rutas, nav items, role/branch matrix, deep links
│   ├── pages-spec.md                 # 12+ páginas: propósito, secciones, read model, store
│   ├── design-system.md              # Tokens, elevación flat vs shadow-elevated, patterns extraídos de skill
│   └── state-stores.md               # SignalStore por feature: withState/withComputed/withMethods/withProps/entities/rxMethod
└── tasks.md             # Phase 2 output (not created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Api/
│   ├── Program.cs                 # AddServiceDefaults, AddOidcAuthentication, AddDbContexts (sin IdentityDbContext), AddCqrs, AddEndpoints
│   ├── Features/                  # Vertical slices ya existentes + nuevos contracts de lectura para UI (dashboard, workItemDetail)
│   └── Tenant/TenantContext.cs    # sub/tenant_id/roles desde JWT
├── Web/                           # Angular 22.1 standalone + @ngrx/signals
│   ├── src/app/
│   │   ├── core/                  # auth (angular-auth-oidc-client), http interceptors (ProblemDetails, ETag), tenant, error handler
│   │   │   ├── auth/              # OidcConfig, AuthGuard, RoleGuard
│   │   │   ├── interceptors/      # problem-details.interceptor, etag.interceptor, tenant.interceptor
│   │   │   └── layout/            # shell: sidebar/nav (Tier N → Tier 2 active), topBar (search Tier 1, avatar, notifications badge)
│   │   ├── features/
│   │   │   ├── dashboard/         # dashboard.store.ts + dashboard.page.ts (KPI cards Tier 2) — SPEC R3 subtree-filtered
│   │   │   ├── projects/          # projects.store.ts + projects.page.ts + project-detail.page
│   │   │   ├── kanban/            # kanban.store.ts + kanban.page.ts (drag-drop, state machine via API)
│   │   │   ├── work-item-detail/  # work-item-detail.store.ts + work-item-detail.page.ts (progress explanation link, metrics, subtasks, docs, AI gated)
│   │   │   ├── my-tasks/          # my-tasks.store.ts + page (assignee == me)
│   │   │   ├── team-tasks/        # team-tasks.store.ts + page (subtree, Manager only)
│   │   │   ├── planning/          # planning.store.ts + page (milestones, Planning per SPEC-004)
│   │   │   ├── documents/         # documents.store.ts + documents.page + document-detail.page (classification, versions)
│   │   │   ├── ai-queue/          # ai-queue.store.ts + ai-queue.page (Generated→Pending Review per XI)
│   │   │   ├── notifications/     # notifications.store.ts + page (InApp, per SPEC-008)
│   │   │   ├── audit/             # audit.store.ts + audit.page (append-only trail, filterable)
│   │   │   ├── admin/             # admin.store.ts + admin.page (org hierarchy, role management)
│   │   │   ├── search/            # search.store.ts + search.page (tenant-filtered)
│   │   │   └── organization/      # org-hierarchy.page (tree, unbounded depth)
│   │   ├── shared/
│   │   │   ├── ui/                # controles reutilizables Tier 1/2: kpi-card, list-card, badge, button (pill 999px), input (18px), pagination, filter-pill, search-bar (Tier 1), avatar-row, chart-card, timeline, modal (Tier 2)
│   │   │   ├── tokens/            # tokens.scss / tailwind config mapeando skill references/tokens.md (Background #F7F7F6, Card #FFFFFF shadow 0 8px 24px, Border #ECECEC, radius 24px/14px/999px, Inter, 8px grid)
│   │   │   └── pipes/             # auth-gated directive (*hasPermission)
│   │   └── app.routes.ts          # lazy routes 16 entries, guards RoleGuard (hides vs API deny), deep links
│   ├── src/environments/          # OIDC authority/audience via Aspire env
│   └── package.json               # @ngrx/signals, @ngrx/signals/entities, @ngrx/signals/rxjs-interop
├── Modules/                       # ya existentes, sin cambios salvo nuevos queries/contracts de lectura si faltan (dashboard KPIs)
│   ├── Organization/ (hierarchy)
│   ├── Projects/Work Management (WorkItem, WorkItemStatus)
│   ├── Documents/ (Document, classification)
│   ├── AiProcessing/ (LlmReview queue)
│   ├── Notifications/ (Notification Preference)
│   └── Audit/ (AuditEntry)
└── OroKanban.AppHost/
    └── AppHost.cs                 # postgres, rabbitmq, redis, identity-api, api, web (AddNpmApp)
```

**Structure Decision**: Web Angular standalone con `src/Web/src/app/{core,features/{12},shared/{ui,tokens}}` y `Api` con slices `IEndpoint` — respeta `src/Web/package.json` ya con Angular 22 + `@ngrx/signals` y `AppHost` con `AddNpmApp`. Toda nueva página es lazy route con `SignalStore` propio; todo control reutilizable vive en `shared/ui` y respeta `Tier 1` (flat, no shadow: search, pills, badges, inputs, icon buttons) vs `Tier 2` (shadow `0 8px 24px`: KPI cards, list cards, active nav pill, modals) de `minimal-ui-design-system/references/`. Estado nunca en `BehaviorSubject`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
