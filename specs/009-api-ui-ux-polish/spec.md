# Feature Specification: 009 Polish — Aterrizaje UI/UX (Convergencia)

**Feature Branch**: `009-api-ui-ux-polish`
**Created**: 2026-09-03
**Status**: Draft
**Depends on**: `009-api-ui-ux` (BC-10 Platform)
**Input**: Backlog de cierre para aterrizar las 12+4 vistas ya scaffolded. No añade dominio nuevo; convierte `partial/missing/contradicts` de `specs/009-api-ui-ux/tasks.md: T063-T074` en vertical slices demoables cumpliendo Constitución XVI/XIX/XXII y skills `minimal-ui-design-system` + `ngrx-signal-store`.

## Objetivo

Hacer que `009` pase de `scaffolding` a `Definition of Done` real: stores con `rxMethod+switchMap`, páginas sin `hardcoded`, contratos `Paged<T>`+`ProblemDetails`+`ETag 409` verificables, y `quickstart.md` 5min verde end-to-end. Sin este polish, cualquier spec `010+` construye sobre base inestable.

## User Scenarios

### US-P1 - Stores conformes a ngrx-signal-store (R6 / T073)

**As** dev frontend **I want** los 5 stores core migrados a `rxMethod` **So that** se cumpla `FR-009/SC-007` y no haya `toPromise`.

**Independent Test**: `grep -L rxMethod` sobre `*.store.ts` → 0. `vitest --run --include="**/*.store.spec.ts"` verde.

**Acceptance**:
1. **Given** `DashboardStore`, `ProjectsStore`, `KanbanBoardStore`, `NotificationsStore`, `WorkItemDetailStore`, **When** se inspecciona código, **Then** usan `signalStore(withState,withEntities?,withComputed,withMethods(rxMethod+switchMap+tapResponse+patchState),withProps?,withHooks?)` + `withRequestStatus`, sin `async/await toPromise`.
2. **Given** `load` disparado 2 veces rápido, **When** segundo `switchMap` entra, **Then** cancela el primero (no `mergeMap` leak) — verificado en `kanban-board.store.spec.ts`.
3. **Given** API retorna `ProblemDetails 500`, **When** `load` falla, **Then** `store.error()` seteado y UI muestra `toast + retry`, no blank.

### US-P2 - Dashboard a datos reales subtree (R3 / T066)

**As** Manager **I want** KPI cards con `GET /api/dashboard/kpis` subtree **So that** `SC-004` 0% leakage se cumpla y desaparezca `24/128` hardcoded.

**Acceptance**:
1. **Given** 2 managers en ramas disjuntas (seed 3 projects c/u, 2 overdue solo en A), **When** cada uno hace `GET /api/dashboard/kpis` con `sub` distinto, **Then** `Overdue A=2, B=0` y `MyProjects=3` cada uno (integration test `DashboardSubtreeTests`).
2. **Given** `Contributor`, **When** abre dashboard, **Then** KPI manager ocultos (`*hasPermission`).
3. **Given** dashboard render, **When** inspeccionado, **Then** `kpi-card Tier2 24px shadow 0 8px 24px` + `chart-card` + `list-card` usan `tokens.scss` vars, no hex.

### US-P3 - Projects Create aterrizado (T067 + gap botón)

**As** Manager **I want** crear Project desde UI **So that** el `POST /api/projects` ya existente sea usable.

**Acceptance**:
1. **Given** `Projects` lista, **When** render, **Then** hay `button.btn-primary New Project 999px Tier1` + `search-bar Tier1 18px` + `filter-pill` + `pagination` (envelope).
2. **Given** click `New Project`, **When** modal Tier2 abre, **Then** form con `Name 3..200, Status/Priority/Criticality Enumeration, DueDate, Description` + selector `Owner/Manager` **limitado a subtree** (opciones = `IManagementHierarchy.GetSubtreeIds(currentUser)`; no input GUID libre) + validación `400 ProblemDetails` inline. Si selector vacío, defaults `OwnerId=ManagerId=sub`.
3. **Given** submit válido, **When** `ProjectsStore.create()` hace `POST /api/projects` con `tenant_id`+`OwnerId/ManagerId` derivados server-side de `TenantContext` (client hints solo dentro de subtree, server re-valida XV/XIX via `IAuthorizationEvaluator`), **Then** `201 Created` mapea a `ProjectResponse`, `setEntity` + `setFulfilled`, lista refresca sin reload; stale/invalid → `ProblemDetails` toast y form preserva edits. Intento con `OwnerId` fuera de subtree → `403 ProblemDetails`.

### US-P4 - Concurrency + ProblemDetails global (R1 / T058/T060)

**As** usuario **I want** conflictos `409` visibles sin pérdida **So that** `SC-002` 95% retry sin data loss.

**Acceptance**:
1. **Given** 2 tabs edit mismo work item `v5`, **When** segunda guarda con `v4` stale (`If-Match`/`version`), **Then** API `409 ProblemDetails {currentVersion:5}` y UI muestra `concurrency-modal` con `Reload/Merge`, ediciones preservadas.
2. **Given** cualquier lista falla (`filter=bad` → `400`), **When** error llega, **Then** `problem-details.interceptor.ts` mapea a toast global (`title/detail/code`) y store `error` signal, no silent fail.
3. **Given** drag Kanban inválido (`Completed→Backlog`), **When** `PUT /api/work-items/:id/status` retorna `409`, **Then** UI revierte card + toast.

### US-P5 - Paginación edge + Tenant isolation (T059/T057)

**As** auditor **I want** paginación y aislamiento tenant correctos **So that** `SC-008` 0% cross-tenant.

**Acceptance**:
1. **Given** `page=999` más allá de total, **When** `GET /api/projects?page=999`, **Then** `items=[] total` unchanged `Link` absent, no `500`.
2. **Given** Search/Dashboard como tenant X, **When** query con `q`, **Then** nunca retorna rows de tenant Y (integration `TenantIsolationTests`).

### US-P6 - Design system audit + Headers consistentes (T061/T074)

**As** reviewer **I want** tokens/elevación verificables **So that** `SC-006` 100% tokens.

**Acceptance**:
1. **Given** `pnpm scripts/audit-tokens.mjs` corre, **When** escanea `src/Web`, **Then** 0 hard-coded `#hex`/`box-shadow`/`8px` fuera de `tokens.scss`/`layout.scss`.
2. **Given** cualquier vista (Projects, MyTasks, Planning, Documents, Search, AI Queue, Audit, Admin), **When** render, **Then** tiene `page-header title/subtitle` como Dashboard, `topBar flat`, `list-card Tier2`, `badge Tier1 999px`.

### US-P7 - Navegación Polish (T063/T064 - T071/T072 diferidos con ADR)

**As** usuario **I want** logout + theme toggle **So that** shell completo.

**Acceptance**:
1. **Given** sidenav footer, **When** render, **Then** `Logout` → `AuthService.logout()` via `OidcSecurityService` + redirect `/login`.
2. **Given** sidenav/topBar, **When** `Theme toggle light/dark`, **Then** persiste `localStorage theme` y swap CSS vars (`bg #F7F7F6 ↔ dark`).
3. **Decisión**: `T071 Search` y `T072 Notifications` como items sidebar vs topBar se **difieren** — requieren ADR-0xx (contradice `navigation-and-access.md`). En polish se mantiene spec actual (sidebar `/search`, `/notifications` visibles) y se **añaden complements** topBar: `search-bar Tier1 18px` que hace `router.navigate(['/search'], {queryParams:{q}})` y `bell icon Tier1` con `unreadCount` badge polling `NotificationsStore.unreadCount` — **sin eliminar** rutas sidebar.

## Requirements

- **FR-P1**: 5 stores core deben usar `rxMethod+switchMap+tapResponse` (no `toPromise`), `withRequestStatus`, y tests `store.spec.ts` verdes.
- **FR-P2**: Dashboard consume `GET /api/dashboard/kpis` subtree (`IManagementHierarchy.GetSubtreeIds`) — sin hardcoded.
- **FR-P3**: `Projects` expone `New Project` modal Tier2 + `ProjectsStore.create()` → `POST /api/projects` (`Name, Status, Priority, Criticality, DueDate, Description` + `OwnerId/ManagerId` **server-derived de `sub/tenant_id` y re-validados contra `IManagementHierarchy` subtree** — client no puede inyectar GUID arbitrario, `403` si fuera de subtree) con `Result→HTTP` y `ProblemDetails` inline.
- **FR-P4**: `409/412` con `currentVersion` preserva edits + `problem-details.interceptor` global.
- **FR-P5**: `page=999` → `[]` + tenant isolation 0% leakage.
- **FR-P6**: `audit-tokens.mjs` 0 violaciones + `page-header` en 10+ vistas.
- **FR-P7**: `Logout` + `theme toggle` persistido; topBar **complements** (`search-bar` → navega a `/search?q=` + `bell unreadCount` badge) **sin mover** rutas sidebar `/search` y `/notifications` (permanecen; rework requiere ADR-0xx).

## Success Criteria (medibles)

- `SC-P1`: `grep -L rxMethod src/Web/src/app/features/*/*.store.ts` == 0 y `pnpm --dir src/Web test -- --run` pasa.
- `SC-P2`: `dotnet test --filter DashboardSubtreeTests` pasa 0% cross-branch leakage y dashboard manual muestra KPIs distintos por manager.
- `SC-P3`: E2E `Create Project` → `201` → aparece en lista paginada + `400` muestra `ProblemDetails` sin perder form.
- `SC-P4`: `ConcurrencyTests` `PUT stale → 409 {currentVersion}` y E2E 2-tabs preserva edits.
- `SC-P5`: `page=999` test verde + `TenantIsolationTests` 0 leakage.
- `SC-P6`: `node src/Web/scripts/audit-tokens.mjs` 0 hard-coded + visual audit 12 vistas Tier ok.
- `SC-P7`: Logout y theme toggle E2E verde; Search/Notifications siguen en sidebar (ADR pendiente documentado).

## Out of Scope (para 010+)

- Rework Search a topBar-only ni Notifications a dropdown-only (requiere ADR-0xx) — polish solo añade complements, no rework.
- Nuevos BCs/entidades; solo convergencia de lo ya scaffolded.
- Real-time SignalR para notifications (solo `unreadCount` badge polling en este slice).

## Constitution Traceability

- XVI APIs Are Contracts (envelope `Paged<T>`, `ProblemDetails`, `ETag` 409)
- XIX Security by Default (UI hides, API denies; tenant+subtree antes de fetch)
- XXII Workspace Skills (minimal-ui-design-system Tier flat vs shadow-elevated, ngrx-signal-store rxMethod/switchMap)
- XV Tenant Aware (subtree + tenant_id en todo query)
