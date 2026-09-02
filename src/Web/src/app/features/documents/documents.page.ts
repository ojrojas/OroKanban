import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { DocumentsStore } from './documents.store';

@Component({
  selector: 'app-documents',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  providers: [DocumentsStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Documents</h1>
      <p class="page-header__subtitle">Classified documents with tenant isolation</p>
    </div>

    <div class="toolbar">
      <div class="search-bar tier-1">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <input placeholder="Search documents..." [value]="store.q()" (input)="store.setQ($any($event.target).value); store.load()" />
      </div>
      <div class="filter-pills">
        <button class="pill" [class.active]="store.filter()==='all'" (click)="store.setFilter('all'); store.load()">All</button>
        <button class="pill" [class.active]="store.filter()==='Confidential'" (click)="store.setFilter('Confidential'); store.load()">Confidential</button>
        <button class="pill" [class.active]="store.filter()==='Public'" (click)="store.setFilter('Public'); store.load()">Public</button>
      </div>
    </div>

    @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between; align-items:center;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="store.load()">Retry</button>
      </div>
    } @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid var(--border);">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Documents</h3>
          <span class="badge">{{ store.total() }} total</span>
        </div>
        @for (d of store.items(); track d.id) {
          <a [routerLink]="['/documents', d.id]" class="row">
            <div class="thumb"></div>
            <div style="flex:1">
              <div style="font-weight:600; font-size:13px; color:var(--text-primary);">{{ d.title ?? d.name ?? 'Document' }}</div>
              <div style="font-size:12px; color:var(--text-secondary);">{{ d.classification ?? 'Unclassified' }} • {{ d.createdAt | date:'short' }}</div>
            </div>
            <span class="badge" [class.confidential]="d.classification==='Confidential'">{{ d.classification ?? '—' }}</span>
          </a>
        }
        @if (store.items().length===0) {
          <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No documents</div>
        }
        <div style="padding:12px 24px; display:flex; justify-content:space-between; align-items:center; border-top:1px solid var(--border); font-size:12px; color:var(--text-muted);">
          <span>Page {{ store.page() }} • {{ store.total() }} total</span>
          <button class="pill" (click)="store.setPage(store.page()+1); store.load()">Next →</button>
        </div>
      </div>
    }
  `,
  styles: [`
    .toolbar { display:flex; gap:12px; flex-wrap:wrap; margin-bottom:var(--gap-widget); }
    .search-bar { display:flex; gap:10px; padding:10px 16px; border-radius:18px; background:var(--flat-bg); border:1px solid var(--border); flex:1; max-width:420px; align-items:center; }
    .search-bar input { flex:1; border:none; outline:none; background:transparent; font-size:13px; }
    .pill { padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer; }
    .pill.active { background: var(--black); color:#fff; }
    .badge { background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; }
    .badge.confidential { background: var(--red-bg); color: var(--red-text); border-color: var(--red-bg); }
    .row { display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); text-decoration:none; align-items:center; }
    .row:hover { background: var(--flat-bg); }
    .thumb { width:36px; height:36px; border-radius:12px; background:var(--border); }
    .skeleton { height:14px; background:var(--border); border-radius:6px; margin:8px 0; }
    .btn-secondary { background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer; }
  `]
})
export class DocumentsPage implements OnInit {
  store = inject(DocumentsStore);
  ngOnInit(): void { this.store.load(); }
}
