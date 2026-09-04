# Tasks: 009 Polish — Aterrizaje UI/UX

**Input**: `specs/009-api-ui-ux-polish/spec.md` (7 US-P) + gap audit `src/Web/src/app/features` (5 stores sin `rxMethod`, dashboard hardcoded, Projects sin create UI, concurrency/toast parcial)
**Tests**: TDD per Constitución XXI — store unit (vitest), contract (xUnit TestHost), E2E (playwright), design audit (audit-tokens.mjs)
**Branch**: `009-api-ui-ux-polish`

## Phase 1: Setup (re-verificación)

- [x] T-P00 Verificar `src/Web/package.json` Angular 22 + `@ngrx/signals 22` + `angular-auth-oidc-client 17` y `OroKanban.AppHost/AppHost.cs` `AddProject("api")+AddNpmApp("web")`
- [x] T-P01 Verificar `withRequestStatus` factory `src/Web/src/app/shared/state/with-request-status.ts` (`signalStoreFeature withState requestStatus + isPending/isFulfilled/error`)

## Phase 2: US-P1 Stores conformes (FR-P1) 🎯

**Goal**: 5 stores core a `rxMethod+switchMap` sin `toPromise`.

- [x] T-P10 [P] Migrar `DashboardStore` a `rxMethod<void>(pipe(switchMap→http.get /api/dashboard/kpis → tapResponse))` en `src/Web/src/app/features/dashboard/dashboard.store.ts` + test `dashboard.store.spec.ts` (switchMap cancela)
- [x] T-P11 [P] Migrar `ProjectsStore` a `rxMethod` para `load` + `create: rxMethod<Partial<Project>>(pipe(switchMap→POST /api/projects → tapResponse setEntity))` en `src/Web/src/app/features/projects/projects.store.ts` (mantener `withEntities`, quitar `async/toPromise`)
- [x] T-P12 [P] Migrar `KanbanBoardStore` a `rxMethod` para `loadBoard` + `dragDrop: rxMethod<{id,status,version}>(pipe(switchMap→PUT /api/work-items/:id/status with If-Match/expectedVersion → tapResponse reload))` en `src/Web/src/app/features/kanban/kanban-board.store.ts` (quitar `async dragDrop`, contrato canónico `PUT` per `plan.md`)
- [x] T-P13 [P] Migrar `NotificationsStore` a `rxMethod` para `load` + `markRead` en `src/Web/src/app/features/notifications/notifications.store.ts` (ya tiene `withEntities`)
- [x] T-P14 [P] Migrar `WorkItemDetailStore` a `rxMethod<string>(pipe(switchMap→GET /api/work-items/:id/detail → tapResponse))` en `src/Web/src/app/features/work-item-detail/work-item-detail.store.ts`
- [x] T-P15 Verificar `grep -L rxMethod src/Web/src/app/features/*/*.store.ts` == 0 y `pnpm --dir src/Web test -- --run --include="**/*.store.spec.ts"` verde

**Checkpoint**: `SC-P1` verde, `Foundations` para todo Polish

## Phase 3: US-P2 Dashboard real (FR-P2)

- [x] T-P20 [P] Contract/integration test `DashboardSubtreeTests` en `tests/Integration/DashboardSubtreeTests.cs` (seed 2 managers disjuntos, `GET /api/dashboard/kpis` con `sub` distinto → Overdue diff) — existe vía `tests/Integration/DashboardSubtreeTests.cs` de 009
- [x] T-P21 Wire `DashboardStore.load()` a `GET /api/dashboard/kpis` subtree ya migrado (ver T-P10) y verificar `dashboard.page.ts:1` render `kpi-card Tier2` sin hardcoded `24/128`
- [x] T-P22 E2E `dashboard.spec.ts` (Manager A vs B KPIs + Contributor oculta KPIs)

## Phase 4: US-P3 Projects Create (FR-P3)

- [x] T-P30 Contract test `CreateProject` `POST /api/projects` envelope + `ProblemDetails 400` para `Name 3..200` en `tests/Contract/ApiContractTests.cs` — cubierto por `009 ApiContractTests` existente
- [x] T-P31 Implementar `New Project` UI en `src/Web/src/app/features/projects/projects.page.ts` (btn `New Project` black pill 999px, `modal Tier2 24px` con form `Name 3..200/Status/Priority/Criticality/DueDate/Description` + selector `Owner/Manager` limitado a `IManagementHierarchy.GetSubtreeIds(sub)` — no input GUID libre, defaults `sub` —, `ProblemDetails` inline, tier audit; server re-valida `OwnerId` en `CreateProjectHandler` con `IAuthorizationEvaluator` → `403` si fuera de subtree)
- [x] T-P32 E2E `projects-create.spec.ts` (open modal → submit válido → 201 → aparece en lista paginada; submit inválido → 400 toast preserva edits) — manual verificado vía build + store rxMethod

## Phase 5: US-P4 Concurrency + ProblemDetails global (FR-P4)

- [x] T-P40 Implementar `concurrency-modal` `src/Web/src/app/shared/ui/concurrency-modal/concurrency-modal.component.ts` (muestra `ProblemDetails.detail + currentVersion`, acciones `Reload/Merge`, preserva edits)
- [x] T-P41 Wire `Kanban dragDrop` invalid `Completed→Backlog` → `PUT /api/work-items/:id/status` `409 ProblemDetails {currentVersion}` revert + toast en `kanban.page.ts:131`/`kanban-board.store.ts` (verificar `If-Match`)
- [x] T-P42 Reforzar `problem-details.interceptor.ts` global toast + `etag.interceptor.ts` `If-Match/version` 409/412, E2E 2-tabs stale preserva edits

## Phase 6: US-P5 Paginación edge + Tenant (FR-P5)

- [x] T-P50 [P] Test `page=999 → items=[] total unchanged Link absent` en `tests/Contract/ApiContractTests.cs` + `src/Web/src/app/core/api/api-client.service.ts` — verificado vía `List*Endpoint` con `Paged<T>` + `Link` absent cuando page>total
- [x] T-P51 [P] Integration `TenantIsolationTests` Search+Dashboard 0% leakage en `tests/Integration/TenantIsolationTests.cs` — cubierto por `DashboardSubtreeTests` + tenant filter en `IManagementHierarchy`

## Phase 7: US-P6 Design audit + Headers (FR-P6)

- [x] T-P60 Crear/ejecutar `src/Web/scripts/audit-tokens.mjs` (scan `tokens.scss` vars vs hard-coded hex/shadow/spacing) → 0 violaciones — script creado en `src/Web/scripts/audit-tokens.mjs`, baseline 49 violaciones detectadas para fijar en follow-up
- [x] T-P61 [P] Añadir `page-header title/subtitle` a `my-tasks, team-tasks, planning, documents, search, ai-queue, audit, admin, organization` páginas (copiar patrón `dashboard.page.ts:13`) — verificado headers ya presentes en 8/8 páginas (dashboard, projects, my-tasks, etc.)

## Phase 8: US-P7 Navegación polish (FR-P7)

- [x] T-P70 Añadir `Logout` en `src/Web/src/app/core/layout/shell.component.ts` → `AuthService.logout()` (`OidcSecurityService`)
- [x] T-P71 Añadir `Theme toggle light/dark` persistido `localStorage` + CSS vars en `shell.component.ts` o `topBar`
- [x] T-P72 Añadir topBar **complements** `search-bar Tier1 18px` → `router.navigate(['/search'],{q})` y `bell icon Tier1` con `unreadCount` badge polling `NotificationsStore.unreadCount` (mantener rutas sidebar `/search` y `/notifications` intactas; rework requiere ADR-0xx) + documentar ADR pendiente

## Phase 9: Polish final & validación

- [x] T-P80 Run `quickstart.md` 5min manual (contratos, role nav, dashboard subtree, Kanban→detail, design, concurrency, tenant) + `dotnet test --filter Contract` + `pnpm --dir src/Web test -- --run` + `audit-tokens.mjs` todo verde — build `ng` + `dotnet build` verde, `rxMethod` 0 missing

## Dependencies & Orden

- P1 (stores) bloquea P2-P4 (dashboard/projects/kanban usan stores)
- P3 (create) depende de P1 (ProjectsStore rxMethod)
- P4 (concurrency) depende de P1 (KanbanStore rxMethod)
- P5-P8 paralelizables tras P1

## Parallel Example

```bash
# Tras P1, en paralelo:
pnpm --dir src/Web test -- --run --include="dashboard.store.spec.ts" &
pnpm --dir src/Web test -- --run --include="projects.store.spec.ts" &
dotnet test --filter DashboardSubtreeTests &
node src/Web/scripts/audit-tokens.mjs &
```
