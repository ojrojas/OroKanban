import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlanningStore } from './planning.store';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-planning',
  standalone: true,
  imports: [CommonModule, BreadcrumbsComponent],
  providers: [PlanningStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Planning'}]" />
    <div class="page-header">
      <h1 class="page-header__title">Planning</h1>
      <p class="page-header__subtitle">Milestones and planning horizons</p>
    </div>

    @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between; align-items:center;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="store.load()">Retry</button>
      </div>
    } @else {
      <div class="tier-2" style="padding:24px 32px;">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Milestones</h3>
          <span class="pill">{{ store.count() }} total</span>
        </div>
        @for (m of store.milestones(); track m.id || m.title) {
          <div class="row">
            <div class="thumb"></div>
            <div style="flex:1">
              <div style="font-weight:600; font-size:13px;">{{ m.title ?? m.name ?? 'Milestone' }}</div>
              <div style="font-size:12px; color:var(--text-secondary);">{{ m.dueDate | date:'shortDate' }} • {{ m.status ?? 'Planned' }}</div>
            </div>
            <span class="badge">{{ m.status ?? 'Planned' }}</span>
          </div>
        }
        @if (store.milestones().length===0) {
          <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No milestones — create one from project planning</div>
        }
      </div>

      <div class="tier-2" style="padding:24px 32px; margin-top: var(--gap-widget);">
        <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Timeline</h3>
        <div style="height:80px; background: var(--flat-bg); border:1px solid var(--border); border-radius:16px; display:grid; place-items:center; color:var(--text-muted); font-size:12px;">Chart placeholder — milestones per sprint</div>
      </div>
    }
  `,
  styles: [`
    .pill { padding:6px 12px; border-radius:999px; font-size:11px; background:var(--flat-bg); border:1px solid var(--border); }
    .badge { background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; }
    .row { display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center; }
    .row:first-child { border-top:none; }
    .thumb { width:36px; height:36px; border-radius:12px; background:var(--border); }
    .skeleton { height:14px; background:var(--border); border-radius:6px; margin:8px 0; }
    .btn-secondary { background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer; }
  `]
})
export class PlanningPage implements OnInit {
  store = inject(PlanningStore);
  ngOnInit(): void { this.store.load(); }
}
