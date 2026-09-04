# Implementation Plan: 009 Polish — Aterrizaje UI/UX

**Branch**: `009-api-ui-ux-polish` | **Date**: 2026-09-03 | **Spec**: `spec.md` | **Parent Plan**: `../009-api-ui-ux/plan.md`
**Status**: Inherits parent plan; this file only documents delta for polish (Constitution XXI gate).

## Parent Inheritance (C1 fix)

This polish **no duplica** `009 plan.md`. Reusa verbatim:

- **Stack**: C# .NET 10 + Angular 22.1 + `@ngrx/signals 22` + `angular-auth-oidc-client 17` + `BuildingBlocks` (CQRS `ISender`, `IEndpoint`, `Result→HTTP`, `Specification<T>`, `AppDbContextBase`, outbox) + `Aspire 13.5` (`AddProject api` + `AddNpmApp web` + external `oroidentityserver` Podman)
- **Architecture**: Vertical Slices `IEndpoint` + `SignalStore` (`signalStore/withState/withComputed/withMethods/withProps/withEntities/withHooks/rxjs-interop`) + `minimal-ui-design-system` Tier flat vs `shadow-elevated` (`0 8px 24px`)
- **Constitution Check**: PASS (I, II, III, IV, V, VII, XV, XVI, XIX, XXII) — polish only aterriza gaps, no new violations.

## Delta for Polish (what changes vs 009)

| Área | 009 entregado | Polish corrige | Files |
|------|---------------|----------------|-------|
| Stores | 5 stores con `async/toPromise` (`dashboard`, `projects`, `kanban-board`, `notifications`, `work-item-detail`) | `rxMethod<void\|string\|Partial>(pipe(switchMap→tapResponse))` + `withRequestStatus` + tests `switchMap cancela` | `dashboard.store.ts`, `projects.store.ts`, `kanban-board.store.ts`, `notifications.store.ts`, `work-item-detail.store.ts` |
| Dashboard | hardcoded `24/128` | `GET /api/dashboard/kpis` subtree `IManagementHierarchy.GetSubtreeIds` | `dashboard.store.ts`, `dashboard.page.ts`, `tests/...DashboardSubtreeTests.cs` |
| Projects | lista solo `GET`, store `create()` sin UI | `modal Tier2 24px` + `button 999px` + form `Name 3..200/Status/Priority/Criticality/DueDate/Description` + `OwnerId/ManagerId` **derivados de `TenantContext`/`sub` (no input libre, XV/XIX)** | `projects.page.ts`, `projects.store.ts` |
| Concurrency | TODO en `kanban.page.ts:131`, interceptors parcial | `concurrency-modal` `Reload/Merge` preserva edits + `problem-details.interceptor` global + `etag.interceptor` `If-Match` → `409 {currentVersion}` | `concurrency-modal.component.ts`, `kanban-board.store.ts`, interceptors |
| Pagination | no test `page=999` | `items=[] total unchanged Link absent` + tenant 0% leakage | `api-client.service.ts`, `PaginationHelper.cs`, `TenantIsolationTests.cs` |
| Design | sin `audit-tokens.mjs`, headers inconsistentes | script audit 0 hard-coded + `page-header` en 8 vistas | `scripts/audit-tokens.mjs`, 8 `*.page.ts` |
| Shell | sin logout/theme | `Logout → OidcSecurityService.logout()` + `theme light/dark localStorage + CSS vars` + topBar **complements** (search→`/search?q=`, bell `unreadCount`) **sin mover rutas sidebar** (ADR pendiente) | `shell.component.ts` |

## Contracts Delta (I1 fix)

- **Kanban status**: canonical `PUT /api/work-items/{id}/status` con `If-Match: W/"{version}"` y body `{targetStatus, expectedVersion}` → `409 ProblemDetails {currentVersion}`. Task T-P12 previously `POST /api/workitems` corrected to this.
- **Projects create**: `POST /api/projects` body `{Name, Status, Priority, Criticality, DueDate, Description}`; `OwnerId/ManagerId/TenantId` injected server from `ctx.User sub/tenant_id` (not client-supplied GUIDs). Validation `Name 3..200` + Enumeration via `IValidator`.

## Test Strategy (TDD XXI)

- **Unit (vitest)**: `*.store.spec.ts` — `withState` init, `withComputed` selectors, `withMethods rxMethod switchMap` cancela, `tapResponse error→store.error()`.
- **Contract (xUnit TestHost)**: `ApiContractTests` (`Paged<T>` + `Link`), `ConcurrencyTests` (`PUT stale →409`), `ProblemDetailsTests` (`400 filter=bad`).
- **Integration**: `DashboardSubtreeTests`, `TenantIsolationTests`.
- **E2E (playwright)**: role nav, dashboard KPIs, `projects-create`, Kanban revert, 2-tabs concurrency, audit tokens.

## Risks

- `withRequestStatus` shared factory ya existe — Polish solo verifica, no re-introduce.
- Search/Notifications routing rework deferred — T-P72 adds complements only, not move.

## Constitution Traceability

- XVI APIs Are Contracts (PUT canonical), XIX Security by Default (server-derived OwnerId), XXII Skills (rxMethod/switchMap, Tier elevation), XV Tenant Aware.
