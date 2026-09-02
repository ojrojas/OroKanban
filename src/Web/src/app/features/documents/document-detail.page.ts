import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-doc-detail',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Document Detail</h1>
      <p class="page-header__subtitle">Versions timeline — immutable</p>
    </div>
    <div class="tier-2" style="padding:24px 32px;">
      <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Versions</h3>
      <div class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">v1 — Initial upload</div><div style="font-size:12px; color:var(--text-secondary);">2026-09-01 • by Admin</div></div><span class="badge">Current</span></div>
    </div>
    <div class="tier-2" style="padding:24px 32px; margin-top: var(--gap-widget);">
      <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Access history</h3>
      <div style="font-size:12px; color:var(--text-muted);">No access yet</div>
    </div>
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;}`]
})
export class DocumentDetailPage {}
