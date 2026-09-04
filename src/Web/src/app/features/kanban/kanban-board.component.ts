import { Component, inject } from '@angular/core';
import { KanbanBoardStore } from './kanban-board.store';

// Kanban board — BC-03 read model
// Contract: GET /api/projects/{projectId}/board (kanban-board-contract.md)
// Design: minimal-ui-design-system — flat pills for filters, elevated cards (shadow 0 8px 24px rgba(0,0,0,.04))

@Component({
  selector: 'app-kanban-board',
  standalone: true,
  imports: [],
  template: `
    @if (store.error()) {
      <div class="kanban-error" style="background:var(--red-bg); border:1px solid var(--border); border-radius:14px; padding:12px 16px; margin:16px 24px; color:var(--red-text); font-size:13px;">
        {{ store.error() }}
      </div>
    }
    @if (store.isPending()) {
      <div class="kanban-loading" style="padding:24px; color:var(--text-secondary); font-size:13px;">Cargando tablero…</div>
    }
    @if (store.isFulfilled() || !store.isPending()) {
      <div class="kanban-board" style="display:flex; gap:16px; background:var(--bg); padding:24px; overflow-x:auto; opacity: {{ store.isPending() ? '0.5' : '1' }};">
        @for (col of store.columns(); track col.statusId) {
          <section class="kanban-column" style="min-width:280px; flex:0 0 280px;">
            <h3 style="font-family:Inter, sans-serif; font-size:13px; font-weight:600; color:var(--text-secondary); margin:0 0 12px; text-transform:uppercase; letter-spacing:0.04em;">
              {{ col.status }} ({{ col.count }})
            </h3>
            <div style="display:flex; flex-direction:column; gap:10px;">
              @for (item of col.items; track item.id) {
                <article class="kanban-card"
                  draggable="true"
                  style="background:var(--card-bg); border:1px solid var(--border); border-radius:18px; padding:14px 16px; box-shadow:var(--shadow-card); transition: box-shadow 200ms ease-in-out;"
                  (dragend)="store.dragDrop({ workItemId: item.id, targetStatus: col.status, expectedVersion: item['version'] })">
                  <div style="font-size:14px; font-weight:500; color:var(--text-primary);">{{ item.title }}</div>
                  <div style="display:flex; gap:6px; margin-top:8px;">
                    <span class="badge" style="background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:2px 8px; font-size:11px; color:var(--text-secondary);">{{ item.criticality }}</span>
                    @if (item.isOverdue) {
                      <span class="badge overdue" style="background:var(--red-bg); border:1px solid var(--border); border-radius:999px; padding:2px 8px; font-size:11px; color:var(--red-text);">Overdue</span>
                    }
                  </div>
                </article>
              }
              @if (col.items.length === 0 && store.isFulfilled()) {
                <div style="font-size:12px; color:var(--text-muted); padding:8px;">Sin elementos</div>
              }
            </div>
          </section>
        }
      </div>
    }
  `,
})
export class KanbanBoardComponent {
  store = inject(KanbanBoardStore);
}
