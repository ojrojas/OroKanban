import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HasPermissionDirective } from '../../shared/pipes/has-permission.directive';
import { DashboardStore } from './dashboard.store';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, HasPermissionDirective],
  providers: [DashboardStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Dashboard</h1>
      <p class="page-header__subtitle">Overview — OroKanban workspace</p>
    </div>

    @if (store.isPending()) {
      <div class="grid-2">
        @for (i of [1,2,3,4]; track i) {
          <div class="tier-2" style="padding:24px 32px; height:120px; background: var(--flat-bg); border: 1px solid var(--border);">
            <div style="height:14px; width:60%; background: var(--border); border-radius: 6px;"></div>
          </div>
        }
      </div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between; align-items:center;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="button secondary" (click)="store.load()">Retry</button>
      </div>
    } @else {
      <div class="kpi-grid">
        @for (k of store.kpis(); track k.key) {
          <div class="tier-2 kpi-card">
            <div class="kpi-top">
              <span class="kpi-label">{{ label(k.key) }}</span>
              @if (k.delta !== undefined) {
                <span [class]="k.delta! >=0 ? 'badge-ok' : 'badge-warn'">{{ k.delta! >0 ? '+' : '' }}{{ k.delta }}%</span>
              }
            </div>
            <div class="kpi-value">{{ k.value }}</div>
            <a class="kpi-link" [routerLink]="k.link || '/projects'">View →</a>
          </div>
        }
        @if (store.kpis().length === 0) {
          <div class="tier-2" style="padding:24px; grid-column: 1 / -1; text-align:center; color:var(--text-muted);">No data — 0 for this subtree</div>
        }
      </div>

      <!-- Only managers see full KPIs, contributors see empty note -->
      <div *hasPermission="'team.read'" class="tier-2" style="padding:24px 32px; margin-top:var(--gap-widget);">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
          <h2 style="font-size:16px; font-weight:700; margin:0;">Subtree summary</h2>
          <span class="pill-tier1">Total {{ store.totalProjects() }} projects</span>
        </div>
        <div style="font-size:13px; color:var(--text-secondary);">Overdue {{ store.overdue() }} • Blocked {{ store.blocked() }}</div>
      </div>
    }

    <div class="tier-2" style="padding:24px 32px; margin-top:var(--gap-widget);">
      <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:20px;">
        <h2 style="font-size:16px; font-weight:700; margin:0;">Recent activity</h2>
        <span class="pill-tier1">Last 7 days</span>
      </div>
      <div style="display:flex; flex-direction:column; gap:0;">
        <div class="row-flat">
          <div class="thumb"></div>
          <div style="flex:1"><div style="font-weight:600; font-size:13px;">Sprint 14 — Planning</div><div style="font-size:12px; color:var(--text-secondary);">Updated 2h ago • by Alex</div></div>
          <span class="badge-ok">Active</span>
        </div>
        <div class="row-flat">
          <div class="thumb"></div>
          <div style="flex:1"><div style="font-weight:600; font-size:13px;">Onboarding docs</div><div style="font-size:12px; color:var(--text-secondary);">Comment added • by Mia</div></div>
          <span class="pill-tier1" style="font-size:11px;">Review</span>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .kpi-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(180px,1fr)); gap: var(--gap-widget); }
    .kpi-card { padding: 20px 24px; display: flex; flex-direction: column; gap: 8px; }
    .kpi-top { display:flex; justify-content:space-between; align-items:center; }
    .kpi-label { font-size:13px; font-weight:500; color:var(--text-secondary); text-transform: capitalize; }
    .kpi-value { font-size:32px; font-weight:700; color:var(--text-primary); letter-spacing:-0.03em; }
    .kpi-link { font-size:12px; color:var(--text-secondary); text-decoration:none; }
    .kpi-link:hover { color: var(--text-primary); }
    .badge-ok { background: var(--green-bg); color: var(--green-text); padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 500; }
    .badge-warn { background: var(--red-bg); color: var(--red-text); padding: 4px 10px; border-radius: 999px; font-size: 12px; font-weight: 500; }
    .pill-tier1 { background: var(--flat-bg); border: 1px solid var(--border); padding: 6px 12px; border-radius: 999px; font-size: 12px; font-weight: 500; color: var(--text-secondary); }
    .row-flat { display: flex; align-items: center; gap: 12px; padding: 12px 0; border-top: 1px solid var(--border); }
    .row-flat:first-child { border-top: none; }
    .thumb { width: 36px; height: 36px; border-radius: 12px; background: var(--border); flex-shrink: 0; }
    .button.secondary { background: var(--flat-bg); border: 1px solid var(--border); border-radius: 999px; padding: 8px 16px; font-size: 12px; cursor: pointer; }
  `]
})
export class DashboardPage implements OnInit {
  store = inject(DashboardStore);
  ngOnInit(): void { this.store.load(); }

  label(key: string): string {
    const map: Record<string,string> = {
      myProjects: 'My Projects', myTeam: 'My Team', mySubManagers: 'My Sub-Managers',
      overdue: 'Overdue', blocked: 'Blocked', critical: 'Critical', atRisk: 'At Risk',
      completed: 'Completed', aiReviewsPending: 'AI Reviews pending', documentReviews: 'Document Reviews',
      activeProjects: 'Active projects', tasksInProgress: 'Tasks in progress'
    };
    return map[key] ?? key;
  }
}
