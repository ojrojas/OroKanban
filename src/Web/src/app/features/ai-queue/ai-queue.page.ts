import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AiQueueStore } from './ai-queue.store';

@Component({
  selector: 'app-ai-queue',
  standalone: true,
  imports: [CommonModule],
  providers: [AiQueueStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">AI Queue</h1>
      <p class="page-header__subtitle">Generated → Pending Review</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; display:flex; justify-content:space-between; border-bottom:1px solid var(--border);"><h3 style="margin:0; font-size:14px; font-weight:700;">Pending reviews</h3><span class="badge">{{store.pendingCount()}} pending</span></div>
        @for (i of store.items(); track i.id) {
          <div class="row">
            <div class="thumb"></div>
            <div style="flex:1"><div style="font-weight:600; font-size:13px;">{{i.title ?? i.operationType ?? 'AI Review'}}</div><div style="font-size:12px; color:var(--text-secondary);">{{i.status ?? 'Pending Review'}} • {{i.documentId ?? ''}}</div></div>
            <button class="btn-primary" (click)="store.approve(i.id)">Approve</button>
          </div>
        }
        @if (store.items().length===0) { <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No pending AI reviews</div> }
      </div>
    }
  `,
  styles: [`.badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .row{display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-primary{background:var(--black); color:#fff; border:none; border-radius:999px; padding:6px 12px; font-size:12px; cursor:pointer;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class AiQueuePage implements OnInit {
  store = inject(AiQueueStore);
  ngOnInit(): void { this.store.load(); }
}
