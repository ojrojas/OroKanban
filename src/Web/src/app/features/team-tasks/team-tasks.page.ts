import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { TeamTasksStore } from './team-tasks.store';

@Component({
  selector: 'app-team-tasks',
  standalone: true,
  imports: [CommonModule, RouterLink],
  providers: [TeamTasksStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Team Tasks</h1>
      <p class="page-header__subtitle">Subtree tasks for your team</p>
    </div>
    <div class="toolbar">
      <div class="filter-pills">
        <button class="pill" [class.active]="store.filter()==='all'" (click)="store.setFilter('all')">All</button>
        <button class="pill" [class.active]="store.filter()==='Blocked'" (click)="store.setFilter('Blocked')">Blocked</button>
        <button class="pill" [class.active]="store.filter()==='Overdue'" (click)="store.setFilter('Overdue')">Overdue</button>
      </div>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; border-bottom:1px solid var(--border); display:flex; justify-content:space-between;"><h3 style="margin:0; font-size:14px; font-weight:700;">Team Tasks</h3><span class="badge">{{store.count()}} total</span></div>
        @for (t of store.filtered(); track t.id) {
          <a [routerLink]="['/work-items', t.id]" class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{t.title ?? t.name}}</div><div style="font-size:12px; color:var(--text-secondary);">{{t.status}}</div></div><span class="badge">{{t.status}}</span></a>
        }
        @if (store.filtered().length===0) { <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No team tasks</div> }
      </div>
    }
  `,
  styles: [`.toolbar{display:flex; gap:12px; margin-bottom:var(--gap-widget);} .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .pill.active{background:var(--black); color:#fff;} .row{display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); text-decoration:none; align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class TeamTasksPage implements OnInit {
  store = inject(TeamTasksStore);
  ngOnInit(): void { this.store.load(); }
}
