import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Project Detail</h1>
      <p class="page-header__subtitle">Members, timeline and history</p>
    </div>
    <div class="tier-2" style="padding:24px 32px;">
      <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Members</h3>
      <div class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">Alex — Owner</div><div style="font-size:12px; color:var(--text-secondary);">Manager</div></div><span class="badge">Owner</span></div>
    </div>
    <div class="tier-2" style="padding:24px 32px; margin-top: var(--gap-widget);">
      <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Timeline</h3>
      <div style="font-size:12px; color:var(--text-muted);">No history yet</div>
    </div>
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;}`]
})
export class ProjectDetailPage {}
