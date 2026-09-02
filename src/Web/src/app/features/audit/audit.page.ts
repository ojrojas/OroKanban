import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AuditStore } from './audit.store';

@Component({
  selector: 'app-audit',
  standalone: true,
  imports: [CommonModule],
  providers: [AuditStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Audit</h1>
      <p class="page-header__subtitle">Append-only trail — filterable</p>
    </div>
    <div class="filter-pills" style="margin-bottom: var(--gap-widget); display:flex; gap:8px;">
      <button class="pill" [class.active]="store.filter()==='all'" (click)="store.setFilter('all')">All</button>
      <button class="pill" [class.active]="store.filter()==='Created'" (click)="store.setFilter('Created')">Created</button>
      <button class="pill" [class.active]="store.filter()==='Updated'" (click)="store.setFilter('Updated')">Updated</button>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; border-bottom:1px solid var(--border);"><h3 style="margin:0; font-size:14px; font-weight:700;">Audit entries</h3></div>
        @for (a of store.items(); track a.id || a.timestamp) {
          <div class="row">
            <div class="thumb"></div>
            <div style="flex:1"><div style="font-weight:600; font-size:13px;">{{a.action ?? a.type}} • {{a.resourceType}}</div><div style="font-size:12px; color:var(--text-secondary);">{{a.actorId}} • {{a.timestamp | date:'short'}}</div></div>
            <span class="badge">{{a.result ?? 'Success'}}</span>
          </div>
        }
        @if (store.items().length===0) { <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No audit entries</div> }
      </div>
    }
  `,
  styles: [`.pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .pill.active{background:var(--black); color:#fff;} .row{display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class AuditPage implements OnInit {
  store = inject(AuditStore);
  ngOnInit(): void { this.store.load(); }
}
