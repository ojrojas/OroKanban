# Contract: Navigation and Access

**App**: `Web` `src/Web/src/app/app.routes.ts` | **Shell**: `src/Web/src/app/core/layout/` (sidebar `250px` + topBar) | **Auth**: `angular-auth-oidc-client` (`oroidentityserver`) → claims `sub`, `tenant_id`, `roles` | **Store**: `navStore` opcional + `RoleGuard` + `*hasPermission`

## Rutas (16, lazy)

```ts
export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', loadComponent: ()=> import('./features/dashboard/dashboard.page').then(m=> m.DashboardPage), canActivate: [AuthGuard] },
  { path: 'organization', loadComponent: ()=> import('./features/organization/org-hierarchy.page').then(m=> m.OrgHierarchyPage), canActivate: [AuthGuard, RoleGuard], data: { roles: ['Manager','Administrator','RootManager'] } },
  { path: 'projects', loadComponent: ()=> import('./features/projects/projects.page').then(m=> m.ProjectsPage), canActivate: [AuthGuard] },
  { path: 'projects/:id', loadComponent: ()=> import('./features/projects/project-detail.page').then(m=> m.ProjectDetailPage), canActivate: [AuthGuard] },
  { path: 'kanban', loadComponent: ()=> import('./features/kanban/kanban.page').then(m=> m.KanbanPage), canActivate: [AuthGuard] },
  { path: 'work-items/:id', loadComponent: ()=> import('./features/work-item-detail/work-item-detail.page').then(m=> m.WorkItemDetailPage), canActivate: [AuthGuard] },
  { path: 'my-tasks', loadComponent: ()=> import('./features/my-tasks/my-tasks.page').then(m=> m.MyTasksPage), canActivate: [AuthGuard] },
  { path: 'team-tasks', loadComponent: ()=> import('./features/team-tasks/team-tasks.page').then(m=> m.TeamTasksPage), canActivate: [AuthGuard, RoleGuard], data: { roles: ['Manager','RootManager','Administrator'] } },
  { path: 'planning', loadComponent: ()=> import('./features/planning/planning.page').then(m=> m.PlanningPage), canActivate: [AuthGuard] },
  { path: 'documents', loadComponent: ()=> import('./features/documents/documents.page').then(m=> m.DocumentsPage), canActivate: [AuthGuard] },
  { path: 'documents/:id', loadComponent: ()=> import('./features/documents/document-detail.page').then(m=> m.DocumentDetailPage), canActivate: [AuthGuard] },
  { path: 'search', loadComponent: ()=> import('./features/search/search.page').then(m=> m.SearchPage), canActivate: [AuthGuard] },
  { path: 'ai-queue', loadComponent: ()=> import('./features/ai-queue/ai-queue.page').then(m=> m.AiQueuePage), canActivate: [AuthGuard], data: { permission: 'ai.review' } },
  { path: 'notifications', loadComponent: ()=> import('./features/notifications/notifications.page').then(m=> m.NotificationsPage), canActivate: [AuthGuard] },
  { path: 'audit', loadComponent: ()=> import('./features/audit/audit.page').then(m=> m.AuditPage), canActivate: [AuthGuard, RoleGuard], data: { permission: 'audit.read' } },
  { path: 'admin', loadComponent: ()=> import('./features/admin/admin.page').then(m=> m.AdminPage), canActivate: [AuthGuard, RoleGuard], data: { roles: ['Administrator','RootManager'] } },
  { path: '**', redirectTo: 'dashboard' }
];
```

Orden en `sidebar-nav` sigue constitution flow: `Dashboard → Organization → Projects → Kanban → My Tasks → Team Tasks → Planning → Documents → Search → AI Queue → Notifications → Audit → Administration` (Login es redirect OIDC, no item).

## Matriz role/branch → visibilidad (hiding es UX, API autoridad)

| Role | Ve | No ve (oculto) | API deniega igual |
|------|----|----------------|-------------------|
| Contributor | Dashboard, Projects, Kanban, My Tasks, Documents, Notifications, Search | Team Tasks, Admin, Audit, Organization (si no Manager) | `GET /api/team-tasks` → 403, `GET /api/audit/**` → 403 |
| Manager | + Team Tasks, Organization subtree, Dashboard KPIs completos | Admin (si no Administrator) | `GET /api/admin/**` → 403 |
| Auditor | + Audit | Team Tasks (si no Manager) | `PUT /api/work-items/**` → 403 |
| Administrator/RootManager | todo incl. Admin | — | — |

Branch: `Team Tasks`/`Dashboard`/`Audit` filtran por `IManagementHierarchy.GetSubtreeIds(sub)` ilimitado — nunca cliente.

## Directiva

```html
<nav *hasPermission="'audit.read'">Audit</nav>
<button *hasPermission="'document.approve'">Approve</button>
```

Implementada en `shared/pipes/has-permission.directive.ts` que lee `AuthGuard.roles` del token; si no tiene, no renderiza pero el handler del botón aún haría `POST` → `403`.

## Deep links

`/admin` como Contributor → `RoleGuard` redirige a `/dashboard` (sin flash) + `GET /api/admin` → `403 ProblemDetails`.

## Flujo de uso (constitution)

`Login (OIDC) → Dashboard (KPI subtree) → Organization (elegir unidad) → Projects (lista) → Kanban (drag) → Work Item Detail (progress Why? → explanation) → Planning/Documents/Search → AI Queue (si ai.review) → Notifications (badge) → Audit (si audit.read) → Admin` — siempre navegable por nav o deep link, siempre tenant-filtered.
