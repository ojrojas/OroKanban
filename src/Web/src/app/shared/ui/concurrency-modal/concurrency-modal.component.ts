import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-concurrency-modal',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <div class="modal-overlay" (click)="close.emit()">
        <div class="tier-2 modal" (click)="$event.stopPropagation()">
          <h3 style="margin:0 0 8px; font-size:16px; font-weight:700;">Concurrency conflict</h3>
          <p style="margin:0 0 12px; font-size:13px; color:var(--text-secondary);">{{ detail }}</p>
          @if (currentVersion) {
            <div style="background:var(--flat-bg); border:1px solid var(--border); border-radius:12px; padding:10px 14px; font-size:12px; margin-bottom:12px;">
              Current version: <strong>{{ currentVersion }}</strong> — your edits are preserved.
            </div>
          }
          <div style="display:flex; justify-content:flex-end; gap:10px;">
            <button class="pill" (click)="close.emit()">Close</button>
            <button class="btn-secondary" (click)="merge.emit()">Merge</button>
            <button class="btn-primary" (click)="reload.emit()">Reload</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .modal-overlay { position:fixed; inset:0; background:rgba(0,0,0,0.35); display:grid; place-items:center; z-index:1000; padding:24px; }
    .modal { background:var(--card-bg, #FFFFFF); border-radius:24px; padding:24px 32px; width:min(480px,100%); box-shadow:0 8px 24px rgba(0,0,0,0.08); }
    .pill { padding:6px 12px; border-radius:999px; font-size:12px; font-weight:500; background:var(--flat-bg); border:1px solid var(--border); color:var(--text-secondary); cursor:pointer; }
    .btn-primary { background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer; }
    .btn-secondary { background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:8px 16px; font-size:12px; cursor:pointer; }
  `]
})
export class ConcurrencyModalComponent {
  @Input() visible = false;
  @Input() detail = 'Version is stale, current is newer.';
  @Input() currentVersion: string | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() reload = new EventEmitter<void>();
  @Output() merge = new EventEmitter<void>();
}
