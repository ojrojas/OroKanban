import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrgHierarchyStore } from './org-hierarchy.store';

@Component({
  selector: 'app-org',
  standalone: true,
  imports: [CommonModule],
  providers: [OrgHierarchyStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Organization</h1>
      <p class="page-header__subtitle">Hierarchy — tree unbounded depth</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:24px 32px;">
        @for (n of store.tree(); track n.id) {
          <div class="row"><div class="avatar"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{n.name}}</div><div style="font-size:12px; color:var(--text-secondary);">{{n.type ?? 'Unit'}} • {{n.memberCount ?? 0}} members</div></div><span class="badge">Level {{n.level ?? 0}}</span></div>
        }
        @if (store.tree().length===0) { <div style="padding:24px; text-align:center; color:var(--text-muted); font-size:13px;">No organization data</div> }
      </div>
    }
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .avatar{width:36px; height:36px; border-radius:50%; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class OrgHierarchyPage implements OnInit {
  store = inject(OrgHierarchyStore);
  ngOnInit(): void { this.store.load(); }
}
