import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminStore } from './admin.store';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule],
  providers: [AdminStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Administration</h1>
      <p class="page-header__subtitle">Organization hierarchy and roles</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:24px 32px;">
        <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Organization units</h3>
        @for (u of store.units(); track u.id || u.name) {
          <div class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{u.name ?? u.title}}</div><div style="font-size:12px; color:var(--text-secondary);">{{u.type ?? 'Unit'}}</div></div><span class="badge">{{u.role ?? 'Member'}}</span></div>
        }
        @if (store.units().length===0) { <div style="padding:24px; text-align:center; color:var(--text-muted); font-size:13px;">No units</div> }
      </div>
      <div class="tier-2" style="padding:24px 32px; margin-top:var(--gap-widget);">
        <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Create unit</h3>
        <div style="display:flex; gap:12px;"><input placeholder="Unit name" style="flex:1; padding:10px 16px; border-radius:18px; border:1px solid var(--border);" /><button class="btn-primary">Create</button></div>
      </div>
    }
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .row:first-child{border-top:none;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-primary{background:var(--black); color:#fff; border:none; border-radius:999px; padding:8px 16px; font-size:12px; cursor:pointer;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class AdminPage implements OnInit {
  store = inject(AdminStore);
  ngOnInit(): void { this.store.load(); }
}
