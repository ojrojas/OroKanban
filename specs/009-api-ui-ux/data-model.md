# Data Model: API, UI and User Experience

**Feature**: 009-api-ui-ux | **Date**: 2026-09-01 | **Schemas**: `Web` read models (proyecciones sobre `orokanban` Postgres + `oroidentityserver` claims) + `Api` DTOs/envelopes; sin nuevas tablas migratorias — reusa `Organization`, `Projects`, `Documents`, `Audit`, `Notifications` ya migrados.

## Read Models (Web consumes via Api)

### 1. ApiContract / Paged<T> / ProblemDetails

**Envelope para toda lista (R1/XVI):**

| Campo | Tipo | Regla |
|-------|------|-------|
| `items` | `T[]` | DTOs estables, nunca entidades internas |
| `total` | `number` | `CountAsync(spec)` antes de paginar |
| `page` | `number` | 1.. (default 1) |
| `pageSize` | `number` | 1..100 (default 20) |
| `Link` | `header` | `rel=next` si `page*pageSize < total` |

**ProblemDetails** (`BuildingBlocks.ServiceDefaults.Endpoints.ResultExtensions`): `{type, title, detail, status, code}` mapeado desde `Error` (`Validation→400`, `NotFound→404`, `Conflict→409`, `Forbidden→403`, `Unauthorized→401`). Todo error pasa por `GlobalExceptionHandler`.

**Concurrencia:** `ETag: "W/\"<version>\""` + `If-Match` o campo `version` en body (`RowVersion` base64). Stale → `409 Conflict` / `412 Precondition Failed` con `ProblemDetails.detail` incluye `currentVersion`.

### 2. NavigationItem / Route

| Campo | Tipo | Regla |
|-------|------|-------|
| `path` | `string` | `/dashboard`, `/projects`, `/kanban`, `/work-items/:id`, `/my-tasks`, `/team-tasks`, `/planning`, `/documents`, `/documents/:id`, `/search`, `/ai-queue`, `/notifications`, `/audit`, `/admin`, `/organization` |
| `label` | `string` | `Dashboard`, `Projects`… |
| `icon` | `string` | nombre icono (flat, no shadow salvo active) |
| `requiredPermission` | `string?` | `null` = todos, `audit.read`, `organization.manage`, `project.create` etc. |
| `requiresManager` | `boolean` | `true` para `team-tasks`, `admin` |
| `visible` | `computed` | `hasPermission(roles, permission) && inSubtree` — UI oculta, API re-valida |

**Guard:** `RoleGuard` + `*hasPermission` directive en `nav`/`topBar`.

### 3. View (12 mínimas + 4 auxiliares)

| View | Ruta lazy | Store | Read model principal |
|------|-----------|-------|----------------------|
| `Dashboard` | `/dashboard` | `dashboardStore` | `DashboardKPI[]` subtree |
| `Projects` | `/projects` | `projectsStore` | `ProjectResponse[]` paginado |
| `Project Detail` | `/projects/:id` | `projectDetailStore` | `ProjectResponse` + miembros |
| `Kanban` | `/kanban?project=:id` | `kanbanStore` | `WorkItemResponse[]` por `status` |
| `Work Item Detail` | `/work-items/:id` | `workItemDetailStore` | `WorkItemDetailAggregate` |
| `My Tasks` | `/my-tasks` | `myTasksStore` | `WorkItemResponse[]` where `assignee==sub` |
| `Team Tasks` | `/team-tasks` | `teamTasksStore` | `WorkItemResponse[]` where `assignee in subtree` |
| `Planning` | `/planning` | `planningStore` | `Milestone[]` + `Metric` |
| `Documents` | `/documents` | `documentsStore` | `DocumentResponse[]` paginado |
| `Document Detail` | `/documents/:id` | `documentDetailStore` | `Document` + `DocumentVersion[]` |
| `Search` | `/search?q=` | `searchStore` | `SearchResult[]` tenant-filtered |
| `AI Queue` | `/ai-queue` | `aiQueueStore` | `LlmReview[]` `Generated→Pending Review` |
| `Notifications` | `/notifications` | `notificationsStore` | `Notification[]` + `unreadCount` |
| `Audit` | `/audit` | `auditStore` | `AuditEntry[]` append-only |
| `Administration` | `/admin` | `adminStore` | `OrganizationUnit[]`, `Role[]` |
| `Organization` | `/organization` | `orgStore` | `OrganizationUnit` tree ilimitado |

Todas lazy en `app.routes.ts` con `canActivate: [AuthGuard, RoleGuard]`.

### 4. DashboardKPI

| Campo | Tipo | Regla |
|-------|------|-------|
| `key` | `enum` | `myProjects, myTeam, mySubManagers, overdue, blocked, critical, atRisk, completed, aiReviewsPending, documentReviews` |
| `value` | `number` | `CountAsync(spec con IManagementHierarchy subtree)` |
| `delta` | `number?` | badge Tier 1 (verde/rojo) vs período |
| `link` | `string` | ruta filtrada a la vista correspondiente |

**Cálculo:** Nunca hard-coded depth — `IManagementHierarchy.GetSubtreeIds(currentUserId)` ilimitado.

### 5. WorkItemDetailAggregate (R4 + XII + XIV)

Composición read model:

| Sección | Campos | Regla |
|---------|--------|-------|
| `workItem` | `id, title, description, responsibleId, managerId, status, version` | status máquina `Backlog→…→Completed` |
| `progress` | `percent: number, explanation: { subtasksDone/total, weighted, evidenceApproved, metrics }` | link `Why?` expande `ProgressExplanation` per SPEC-004 |
| `metrics` | `WorkItemMetric[]` | configurables |
| `subtasks` | `WorkItem[]` hijos |  |
| `dependencies` | `WorkItemDependency[]` |  |
| `documents` | `DocumentResponse[]` | tenant-filtered |
| `history` | `AuditEntry[]` + `WorkItemHistory[]` |  |
| `comments` | `Comment[]` |  |
| `aiInfo` | `LlmReview[]?` | visible solo si `hasPermission('ai.review')`, sino `403` |

### 6. DesignToken (de skill `references/tokens.md`)

| Token | Valor | Uso |
|-------|-------|-----|
| `bg` | `#F7F7F6` | Tier 0 |
| `cardBg` | `#FFFFFF` | Tier 2 |
| `flatBg` | `#FFFFFF` `#FDFDFD` | Tier 1 |
| `border` | `#ECECEC` | hairline |
| `textPrimary` | `#111111` | headings |
| `textSecondary` | `#777777` | body/nav inactivo |
| `textMuted` | `#A9A9A9` | timestamps |
| `green` | `#63D471` | badge tint `bg #E8F9EC text #2FA84A` |
| `red` | `#F26B6B` | badge tint `bg #FCE8E8 text #C0392B` |
| `radiusCard` | `24px` | Tier 2 cards |
| `radiusPill` | `14px` | nav pill active |
| `radiusButton` | `999px` | CTA |
| `radiusInput` | `18px` | search/input |
| `shadowCard` | `0 8px 24px rgba(0,0,0,.04)` | Tier 2 resting |
| `shadowHover` | `0 12px 32px rgba(0,0,0,.06)` | Tier 2 hover |
| `font` | `Inter` | Bold headings / Regular body / Medium labels |
| `grid` | `8px` | `32px` outer, `24px` gap, `24-32px` card padding |

**Elevación (crítica):** `Tier 0` fondo; `Tier 1` flat (`search-bar`, `filter-pill`, `badge`, `input`, icon buttons top bar) — `bg #FFFFFF` + opcional `border #ECECEC` **sin shadow**; `Tier 2` elevated (`kpi-card`, `list-card`, `chart-card`, `active nav pill`, `modal`, floating buttons) — `shadow`; `Tier N` nav inactivo transparente `#777777`, hover `tint #F0F0EF` sin shadow.

### 7. SignalStore State Shape (per skill)

**Genérico por feature:**

| Prop | Tipo | Fuente |
|------|------|--------|
| `items` | `Signal<Entity[]>` | `withEntities<Entity>()` |
| `loading` | `Signal<boolean>` | `withState` |
| `error` | `Signal<string|null>` | `withState` + `withRequestStatus` |
| `filter` | `Signal<Filter>` | `withState` |
| `selectedId` | `Signal<EntityId|null>` | `withState` |
| `filtered` | `Signal<Entity[]>` | `withComputed` sobre `items`+`filter` |
| `isPending/isFulfilled` | `Signal<boolean>` | `withComputed` sobre `requestStatus` |

**Métodos:** `load: rxMethod<void>(switchMap→Api→patchState(setAllEntities))`, `loadById: rxMethod<EntityId>`, `update: rxMethod<Payload>(switchMap→Api.put con version→tapResponse→patchState)`, `setFilter`. **Hooks:** `withHooks({onInit: store=> store.load()})`. **Props:** `withProps(()=> ({ _api: inject(ApiClient) }))`.

**Entidades:** `WorkItem`, `Project`, `Document`, `AuditEntry`, `Notification`, `LlmReview` mapeadas a DTOs.

### 8. Control Reutilizable (shared/ui)

| Control | Tier | Props |
|---------|------|-------|
| `sidebar-nav` | container Tier 0, item Tier N → active Tier 2 | `items: NavItem[], activePath, collapsed` |
| `top-bar` | flat | `searchQuery, onSearch, notificationsCount` |
| `kpi-card` | Tier 2 | `kpis: DashboardKPI[]` |
| `list-card` | Tier 2 container, rows flat | `title, filterPill, rows: {thumb, title, subtitle, value, badge}[]` |
| `chart-card` | Tier 2 | `title, data, highlightedIndex, tooltip` |
| `badge` | Tier 1 | `label, tone: green|red|gray, size: sm` |
| `button` | primary black flat / secondary Tier 1 | `label, variant, disabled` |
| `input` | Tier 1 | `placeholder, value, error` |
| `search-bar` | Tier 1 | `query, onQuery` |
| `filter-pill` | Tier 1 | `options, selected, onChange` |
| `pagination` | flat | `page, pageSize, total, onPage` + header `Link` |
| `avatar-row` | flat | `avatars: {src,name}[], onViewAll` |
| `timeline` | card Tier 2, items flat | `entries: {timestamp, actor, action}[]` |
| `modal` | Tier 2 | `open, title, onClose` |
| `progress-explanation` | nested Tier 2 tooltip | `percent, breakdown` |

Todos consumen `tokens.scss` CSS vars, no hex hardcodeado.

## Relaciones

- `NavigationItem 1—* View` via `path`/`requiredPermission`
- `View *—1 SignalStore` (cada vista tiene store propio)
- `DashboardKPI *—1 SignalStore.dashboardStore` (agrega `WorkItem`+`Document`+`LlmReview` queries filtradas por `IManagementHierarchy`)
- `WorkItemDetailAggregate 1—1 WorkItem` + `* Subtask` + `* Document` + `* AuditEntry`
- `ApiContract Paged<T>` es usado por `list-card` + `pagination` en todas las listas
- `DesignToken *—* Control` (todo control mapea a tokens Tier 1/2)
- `SignalStore` consume `ApiClient` (http) que consume `Api` `IEndpoint` que aplica `Specification<T>` con `tenant_id`+subtree

