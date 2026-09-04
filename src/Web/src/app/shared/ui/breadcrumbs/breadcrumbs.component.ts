import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';

export interface Crumb { label: string; link?: string; }

@Component({
  selector: 'app-breadcrumbs',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <nav class="breadcrumbs" aria-label="Breadcrumb">
      @for (c of crumbs; track c.label; let last=$last) {
        @if (c.link && !last) {
          <a [routerLink]="c.link" class="crumb-link">{{ c.label }}</a>
          <span class="sep">/</span>
        } @else {
          <span class="crumb-current" [class.last]="last">{{ c.label }}</span>
          @if (!last) { <span class="sep">/</span> }
        }
      }
    </nav>
  `,
  styles: [`
    .breadcrumbs { display:flex; align-items:center; gap:6px; font-size:12px; color:var(--text-muted); margin-bottom:12px; flex-wrap:wrap; }
    .crumb-link { color:var(--text-secondary); text-decoration:none; padding:4px 8px; border-radius:999px; background:var(--flat-bg); border:1px solid var(--border); }
    .crumb-link:hover { color:var(--text-primary); background:var(--card-bg); }
    .crumb-current { font-weight:600; color:var(--text-primary); padding:4px 8px; }
    .sep { color:var(--text-muted); font-size:11px; }
  `]
})
export class BreadcrumbsComponent {
  @Input() crumbs: Crumb[] = [];
}
