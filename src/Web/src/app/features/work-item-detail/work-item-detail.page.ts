import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { WorkItemDetailStore } from './work-item-detail.store';

@Component({
  selector: 'app-work-detail',
  standalone: true,
  imports: [CommonModule],
  providers: [WorkItemDetailStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Work Item Detail</h1>
      <p class="page-header__subtitle">Progress with explanation link</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="load()">Retry</button></div> }
    @else if (store.item()) {
      <div class="tier-2" style="padding:24px 32px;">
        <div style="display:flex; justify-content:space-between; align-items:center;">
          <h3 style="margin:0; font-size:14px; font-weight:700;">{{store.item()?.title ?? 'Work Item'}}</h3>
          <span class="badge">In Progress</span>
        </div>
        <div style="margin-top:12px; font-size:13px; color:var(--text-secondary);">{{store.item()?.title ?? 'No description'}}</div>
        <div style="margin-top:16px; padding:12px; background:var(--flat-bg); border:1px solid var(--border); border-radius:12px;">
          <div style="display:flex; justify-content:space-between; align-items:center;">
            <span style="font-weight:600; font-size:13px;">Progress {{store.item()?.progress ?? 65}}%</span>
            <button class="pill" (click)="showExplain.set(!showExplain())">Why?</button>
          </div>
          @if (showExplain()) {
            <div style="margin-top:12px; font-size:12px; color:var(--text-secondary);">
              <div>{{ store.progressExplanation()?.breakdown ?? 'subtasks 2/3, evidence approved, metric X' }}</div>
            </div>
          }
        </div>
        <div style="margin-top:16px; display:grid; grid-template-columns: 1fr 1fr; gap:16px;">
          <div><h4 style="font-size:12px; font-weight:700;">Subtasks</h4><div style="font-size:12px; color:var(--text-muted);">{{ store.item()?.subtasks?.length ?? 0 }} subtasks</div></div>
          <div><h4 style="font-size:12px; font-weight:700;">Dependencies</h4><div style="font-size:12px; color:var(--text-muted);">No dependencies</div></div>
        </div>
      </div>
    } @else {
      <div class="tier-2" style="padding:32px; text-align:center; color:var(--text-muted);">Select a work item</div>
    }
  `,
  styles: [`.badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class WorkItemDetailPage implements OnInit {
  store = inject(WorkItemDetailStore);
  showExplain = signal(false);
  private route = inject(ActivatedRoute);
  ngOnInit(): void { this.load(); }
  load(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    if (id) (this.store as any).load(id);
  }
}
