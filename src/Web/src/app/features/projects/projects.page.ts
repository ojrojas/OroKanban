import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProjectsStore } from './projects.store';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  providers: [ProjectsStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Projects</h1>
      <p class="page-header__subtitle">Manage and browse all projects in your subtree</p>
    </div>

    <div class="toolbar">
      <div class="search-bar tier-1">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <input placeholder="Search projects..." [value]="store.filter()" (input)="store.setFilter($any($event.target).value)" />
      </div>
      <div class="filter-pills">
        <button class="pill" [class.active]="store.filter()===''" (click)="store.setFilter('')">All</button>
        <button class="pill" [class.active]="store.filter()==='active'" (click)="store.setFilter('active')">Active</button>
        <button class="pill" [class.active]="store.filter()==='archived'" (click)="store.setFilter('archived')">Archived</button>
      </div>
      <button class="btn-primary" (click)="store.load()">Refresh</button>
    </div>

    @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div><div class="skeleton" style="width:60%"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between; align-items:center;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="store.load()">Retry</button>
      </div>
    } @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid var(--border);">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Projects</h3>
          <span class="badge">{{ store.total() }} total</span>
        </div>
        @for (p of store.filtered(); track p.id) {
          <a [routerLink]="['/projects', p.id]" class="row">
            <div class="thumb"></div>
            <div style="flex:1">
              <div style="font-weight:600; font-size:13px; color:var(--text-primary);">{{ p.name }}</div>
              <div style="font-size:12px; color:var(--text-secondary);">{{ p.status }}</div>
            </div>
            <span class="badge">{{ p.status }}</span>
          </a>
        }
        @if (store.filtered().length===0) {
          <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No projects found</div>
        }
        <div style="padding:12px 24px; display:flex; justify-content:space-between; align-items:center; border-top:1px solid var(--border); font-size:12px; color:var(--text-muted);">
          <span>Page {{ store.page() }} • {{ store.total() }} total</span>
          <button class="pill" (click)="store.load()">Next →</button>
        </div>
      </div>
    }
  `,
  styles: [`
    .toolbar { display:flex; gap:12px; align-items:center; flex-wrap:wrap; margin-bottom: var(--gap-widget); }
    .search-bar { display:flex; align-items:center; gap:10px; padding:10px 16px; border-radius: var(--radius-input); background: var(--flat-bg); border:1px solid var(--border); flex:1; max-width:420px; }
    .search-bar input { flex:1; border:none; outline:none; background:transparent; font-size:13px; color:var(--text-primary); }
    .filter-pills { display:flex; gap:8px; }
    .pill { padding:6px 12px; border-radius:999px; font-size:12px; font-weight:500; background: var(--flat-bg); border:1px solid var(--border); color:var(--text-secondary); cursor:pointer; }
    .pill.active { background: var(--text-primary); color:#fff; border-color: var(--text-primary); }
    .badge { background: var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; font-weight:500; }
    .row { display:flex; align-items:center; gap:12px; padding:12px 24px; border-top:1px solid var(--border); text-decoration:none; }
    .row:first-child { border-top:none; }
    .row:hover { background: var(--flat-bg); }
    .thumb { width:36px; height:36px; border-radius:12px; background: var(--border); }
    .btn-primary { background: var(--black); color:#fff; border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer; }
    .btn-secondary { background: var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; font-size:12px; cursor:pointer; }
    .skeleton { height:14px; background: var(--border); border-radius:6px; margin:8px 0; }
  `]
})
export class ProjectsPage implements OnInit {
  store = inject(ProjectsStore);
  ngOnInit(): void { this.store.load(); }
}
