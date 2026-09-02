import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SearchStore } from './search.store';
import { ActivatedRoute } from '@angular/router';

@Component({
  selector: 'app-search',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [SearchStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Search</h1>
      <p class="page-header__subtitle">Tenant-filtered search across projects, work items and documents</p>
    </div>

    <div class="search-bar tier-1" style="max-width:640px; margin-bottom:16px;">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
      <input placeholder="Search tenant..." [value]="store.q()" (input)="onInput($any($event.target).value)" (keydown.enter)="doSearch()" />
      <button class="pill" (click)="doSearch()">Search</button>
    </div>

    <div class="filter-pills" style="margin-bottom: var(--gap-widget); display:flex; gap:8px;">
      <button class="pill" [class.active]="store.type()==='all'" (click)="store.setType('all'); doSearch()">All</button>
      <button class="pill" [class.active]="store.type()==='project'" (click)="store.setType('project'); doSearch()">Projects</button>
      <button class="pill" [class.active]="store.type()==='workItem'" (click)="store.setType('workItem'); doSearch()">Work Items</button>
      <button class="pill" [class.active]="store.type()==='document'" (click)="store.setType('document'); doSearch()">Documents</button>
    </div>

    @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="doSearch()">Retry</button>
      </div>
    } @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; border-bottom:1px solid var(--border);">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Results</h3>
          <span style="font-size:12px; color:var(--text-muted);">{{ store.count() }} results for "{{ store.q() }}"</span>
        </div>
        @for (r of store.results(); track r.id || r.title) {
          <div class="row">
            <div class="thumb"></div>
            <div style="flex:1">
              <div style="font-weight:600; font-size:13px;">{{ r.title ?? r.name ?? 'Result' }}</div>
              <div style="font-size:12px; color:var(--text-secondary);">{{ r.type ?? r.kind }} • {{ r.subtitle ?? '' }}</div>
            </div>
            <span class="badge">{{ r.type ?? '' }}</span>
          </div>
        }
        @if (store.results().length===0) {
          <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No results — try another query (tenant-filtered)</div>
        }
      </div>
    }
  `,
  styles: [`
    .search-bar { display:flex; gap:10px; padding:10px 16px; border-radius:18px; background:var(--flat-bg); border:1px solid var(--border); align-items:center; }
    .search-bar input { flex:1; border:none; outline:none; background:transparent; font-size:13px; }
    .pill { padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer; }
    .pill.active { background: var(--black); color:#fff; }
    .badge { background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; }
    .row { display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); align-items:center; }
    .thumb { width:36px; height:36px; border-radius:12px; background:var(--border); }
    .skeleton { height:14px; background:var(--border); border-radius:6px; margin:8px 0; }
    .btn-secondary { background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer; }
  `]
})
export class SearchPage {
  store = inject(SearchStore);
  private route = inject(ActivatedRoute);
  constructor(){ const q = this.route.snapshot.queryParamMap.get('q') ?? ''; if (q) { this.store.search(q); } }
  onInput(v:string){ this.store.search(v); }
  doSearch(): void { const el = document.querySelector('.search-bar input') as HTMLInputElement; const q = el?.value ?? ''; this.store.search(q); }
}
