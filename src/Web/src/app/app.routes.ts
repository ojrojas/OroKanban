import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { RoleGuard } from './core/auth/role.guard';
import { CallbackComponent } from './core/auth/callback.component';
import { LogoutCallbackComponent } from './core/auth/logout-callback.component';
import { ShellComponent } from './core/layout/shell.component';

export const routes: Routes = [
  // Auth callbacks — fuera del Shell (sin sidenav/header)
  { path: 'auth/callback', component: CallbackComponent },
  { path: 'auth/logout-callback', component: LogoutCallbackComponent },

  // App protegida — dentro del Shell minimal-ui (sidenav + top-bar Tier1/Tier2)
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.page').then(m => m.DashboardPage) },
      { path: 'organization', loadComponent: () => import('./features/organization/org-hierarchy.page').then(m => m.OrgHierarchyPage), canActivate: [RoleGuard], data: { roles: ['Manager','Administrator','RootManager'] } },
      { path: 'projects', loadComponent: () => import('./features/projects/projects.page').then(m => m.ProjectsPage) },
      { path: 'projects/:id', loadComponent: () => import('./features/projects/project-detail.page').then(m => m.ProjectDetailPage) },
      { path: 'kanban', loadComponent: () => import('./features/kanban/kanban.page').then(m => m.KanbanPage) },
      { path: 'work-items/:id', loadComponent: () => import('./features/work-item-detail/work-item-detail.page').then(m => m.WorkItemDetailPage) },
      { path: 'my-tasks', loadComponent: () => import('./features/my-tasks/my-tasks.page').then(m => m.MyTasksPage) },
      { path: 'team-tasks', loadComponent: () => import('./features/team-tasks/team-tasks.page').then(m => m.TeamTasksPage), canActivate: [RoleGuard], data: { roles: ['Manager','RootManager','Administrator'] } },
      { path: 'planning', loadComponent: () => import('./features/planning/planning.page').then(m => m.PlanningPage) },
      { path: 'documents', loadComponent: () => import('./features/documents/documents.page').then(m => m.DocumentsPage) },
      { path: 'documents/:id', loadComponent: () => import('./features/documents/document-detail.page').then(m => m.DocumentDetailPage) },
      { path: 'search', loadComponent: () => import('./features/search/search.page').then(m=> m.SearchPage) },
      { path: 'ai-queue', loadComponent: () => import('./features/ai-queue/ai-queue.page').then(m=> m.AiQueuePage), canActivate: [RoleGuard], data: { permission: 'ai.review' } },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications.page').then(m=> m.NotificationsPage) },
      { path: 'audit', loadComponent: () => import('./features/audit/audit.page').then(m=> m.AuditPage), canActivate: [RoleGuard], data: { permission: 'audit.read' } },
      { path: 'admin', loadComponent: () => import('./features/admin/admin.page').then(m=> m.AdminPage), canActivate: [RoleGuard], data: { roles: ['Administrator','RootManager'] } },
    ],
  },

  { path: '**', redirectTo: 'dashboard' },
];
