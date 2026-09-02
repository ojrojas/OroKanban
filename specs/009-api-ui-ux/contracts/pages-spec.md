# Contract: Pages Spec (12 + 4)

Cada página: ruta, store, secciones, read model, controles (Tier), vacíos/errores.

## 1. Dashboard (`/dashboard`, `dashboardStore`)

**Propósito:** Manager overview subtree (R3).
**Secciones:** TopBar (search Tier 1 + notifications badge) + grid `KPI cards` (Tier 2, 24px, 2–3 por fila) para `My Projects, My Team (5), My SubManagers (1), Overdue, Blocked, Critical, At Risk, Completed, AI Reviews pending, Document Reviews` + `chart-card` (Tier 2, trend) + `list-card` (Recent activity, Tier 2) + `avatar-row` (flat).
**Store:** `loadKpis(filter: {orgId})` via `GET /api/dashboard/kpis` (subtree).
**Vacío:** sin datos → card `0` con `No data` muted, no error.
**Controles:** `kpi-card`, `chart-card`, `list-card`, `badge`.

## 2. Projects (`/projects`, `projectsStore`)

**List:** `list-card` Tier 2 con rows flat, `filter-pill` Tier 1 (status), `search-bar` Tier 1, `pagination` + `total`. **Detail** (`/projects/:id`): header + `list-card` de miembros + `timeline`.

## 3. Kanban (`/kanban?project=:id`, `kanbanStore`)

**Columnas** `Backlog→Planned→In Progress→Blocked→In Review→Completed` (mismo `WorkItemStatus` enum). Cards Tier 2, drag-drop → `PUT /api/work-items/:id/status` con `version` → `409` si inválido → revert + toast `ProblemDetails`. `withEntities<WorkItem>` en store.

## 4. Work Item Detail (`/work-items/:id`, `workItemDetailStore`)

**Secciones** (R4): header (title, `badge` status Tier 1), grid 2 col: izquierda `description` + `progress` `65%` + `progress-explanation` link → modal Tier 2 con breakdown `subtasks 2/3, evidence, metrics`; `metrics` (badges), `subtasks` list-card, `dependencies` list-card, `documents` list-card (link a `/documents/:id`), `history` timeline, `comments` list-card, `aiInfo` (solo si `ai.review`, sino oculto). `TopBar` actions `Edit` (black pill) si permiso.

## 5. My Tasks (`/my-tasks`, `myTasksStore`)

`list-card` donde `assignee==sub`, `filter-pill` status, `search-bar` q, paginado.

## 6. Team Tasks (`/team-tasks`, `teamTasksStore`, Manager)

Mismo que My Tasks pero `assignee in subtree` — store llama `GET /api/team-tasks?filter=assigneeSubtree`.

## 7. Planning (`/planning`, `planningStore`)

`chart-card` milestones, `list-card` plan configs, versiones.

## 8. Documents (`/documents`, `documentsStore`)

`list-card` con thumb, `badge` classification (`Confidential` rojo tint), `filter-pill` classification, `pagination`. **Detail** (`/documents/:id`): `timeline` versiones (immutable), `badge` status, `list-card` access history.

## 9. Search (`/search?q=`, `searchStore`)

`search-bar` Tier 1 + `filter-pill` type + resultados `list-card` tenant-filtered, nunca cross-tenant.

## 10. AI Queue (`/ai-queue`, `aiQueueStore`, `ai.review`)

`list-card` de `LlmReview` `Generated→Pending Review`, acción `Approve/Reject` (black pill) → `409` si stale.

## 11. Notifications (`/notifications`, `notificationsStore`)

`list-card` `InApp`, badge `unreadCount` en topBar, `filter-pill` type, `pagination`. Click → mark read + link a recurso.

## 12. Audit (`/audit`, `auditStore`, `audit.read`)

`list-card` append-only, `filter-pill` actor/action, `search-bar`, `timeline` para trail. No edit.

## 13. Administration (`/admin`, `adminStore`, `Administrator`)

`list-card` `OrganizationUnit` tree (ilimitado), `badge` role, form Tier 2 card para crear unidad.

## 14. Organization (`/organization`, `orgStore`)

Árbol jerárquico con `avatar-row` + `timeline` de `ManagementRelationship`.

**Patrón común en todas:** `loading→skeleton` (flat), `error→ProblemDetails toast + retry` (secondary button Tier 1), `empty→muted text`.

