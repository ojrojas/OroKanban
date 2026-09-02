import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';

@Component({
  selector: 'app-kanban',
  standalone: true,
  imports: [CommonModule, CdkDropList, CdkDrag],
  template: `
    <div class="page-header" style="flex-shrink:0;">
      <h1 class="page-header__title">Kanban</h1>
      <p class="page-header__subtitle">Backlog → Planned → In Progress → Blocked → In Review → Completed</p>
    </div>
    <div class="kanban-board">
      <div class="kanban-cols">
        @for (col of columns; track col) {
          <div class="kanban-col tier-2">
            <h4 class="col-title">{{col}} <span class="col-count">{{ cards[col]?.length ?? 0 }}</span></h4>
            <div class="col-cards"
                 cdkDropList
                 [cdkDropListData]="cards[col] ?? []"
                 [cdkDropListConnectedTo]="dropListIds"
                 [id]="col"
                 (cdkDropListDropped)="drop($event, col)">
              @for (card of cards[col] ?? []; track card.id) {
                <div class="kanban-card" cdkDrag [cdkDragData]="card">
                  <div class="card-title">{{card.title}}</div>
                  <div class="card-meta">{{card.assignee}} • {{card.id}}</div>
                </div>
              }
              @if ((cards[col]?.length ?? 0) === 0) {
                <div class="drop-zone">Drop here</div>
              }
            </div>
          </div>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; min-height:0; }
    .kanban-board { flex:1; min-height:0; display:flex; flex-direction:column; overflow:hidden; }
    .kanban-cols{
      display:flex; gap:16px; overflow:auto; flex:1; min-height:0;
      padding-bottom:8px; align-items:stretch;
      /* occupy until bottom of screen */
      max-height: calc(100dvh - 220px);
    }
    .kanban-col{
      flex: 0 0 280px;
      min-width:280px; max-width:280px;
      padding:16px; display:flex; flex-direction:column; min-height:0;
    }
    .col-title{ margin:0 0 12px; font-size:13px; font-weight:700; display:flex; justify-content:space-between; align-items:center; }
    .col-count{ background:var(--flat-bg); border:1px solid var(--border); padding:2px 8px; border-radius:999px; font-size:11px; font-weight:500; }
    .col-cards{ flex:1; min-height:0; overflow-y:auto; overflow-x:hidden; display:flex; flex-direction:column; gap:10px; padding-right:4px; }
    .col-cards::-webkit-scrollbar{ width:6px; height:6px; }
    .col-cards::-webkit-scrollbar-thumb{ background: var(--border); border-radius:999px; }
    .kanban-card{ background:var(--flat-bg); border:1px solid var(--border); border-radius:14px; padding:12px; font-size:13px; }
    .card-title{ font-weight:600; color:var(--text-primary); font-size:13px; }
    .card-meta{ font-size:11px; color:var(--text-muted); margin-top:4px; }
    .drop-zone{ min-height:72px; background:var(--flat-bg); border:1px dashed var(--border); border-radius:12px; display:grid; place-items:center; color:var(--text-muted); font-size:12px; margin-top:8px; }
    .kanban-cols::-webkit-scrollbar{ height:8px; width:8px; }
    .kanban-cols::-webkit-scrollbar-thumb{ background: var(--border); border-radius:999px; }
    .cdk-drag-preview{ box-shadow: var(--shadow-hover); border-radius:14px; opacity:0.95; transform: rotate(1deg); }
    .cdk-drag-placeholder{ opacity:0.2; border:1px dashed var(--border); background: var(--flat-bg); }
    .cdk-drag-animating{ transition: transform 200ms ease; }
    .col-cards.cdk-drop-list-dragging .kanban-card:not(.cdk-drag-placeholder){ transition: transform 200ms ease; }
  `]
})
export class KanbanPage {
  columns = ['Backlog','Planned','In Progress','Blocked','In Review','Completed'];
  get dropListIds(): string[] { return this.columns; }
  cards: Record<string, {id:string; title:string; assignee:string}[]> = {
    'Backlog': [
      {id:'WI-101', title:'Setup project baseline', assignee:'Alex'},
      {id:'WI-102', title:'Define metrics model', assignee:'Mia'},
      {id:'WI-103', title:'Create org hierarchy', assignee:'Jon'},
      {id:'WI-104', title:'Draft audit spec', assignee:'Sara'},
      {id:'WI-105', title:'Seed demo data', assignee:'Alex'},
      {id:'WI-106', title:'Configure Aspire', assignee:'Mia'},
      {id:'WI-107', title:'Add OIDC guards', assignee:'Jon'},
      {id:'WI-108', title:'Implement tokens', assignee:'Sara'},
    ],
    'Planned': [
      {id:'WI-201', title:'Sprint 14 planning', assignee:'Alex'},
      {id:'WI-202', title:'Onboarding docs review', assignee:'Mia'},
    ],
    'In Progress': [
      {id:'WI-301', title:'Kanban drag-drop', assignee:'Alex'},
      {id:'WI-302', title:'Work item detail progress', assignee:'Mia'},
      {id:'WI-303', title:'Notifications hub', assignee:'Jon'},
    ],
    'Blocked': [
      {id:'WI-401', title:'Waiting for approval', assignee:'Sara'},
    ],
    'In Review': [
      {id:'WI-501', title:'PR #42 review', assignee:'Alex'},
      {id:'WI-502', title:'Design audit', assignee:'Mia'},
    ],
    'Completed': [
      {id:'WI-601', title:'Initial scaffold', assignee:'Jon'},
      {id:'WI-602', title:'Auth interceptor', assignee:'Sara'},
      {id:'WI-603', title:'Api envelope', assignee:'Alex'},
      {id:'WI-604', title:'Seed organization', assignee:'Mia'},
    ],
  };

  drop(event: CdkDragDrop<{id:string; title:string; assignee:string}[]>, targetCol: string): void {
    const prevCol = event.previousContainer.id;
    const currCol = event.container.id as string;
    const prevList = this.cards[prevCol];
    const currList = this.cards[currCol];
    if (!prevList || !currList) return;

    if (event.previousContainer === event.container) {
      moveItemInArray(currList, event.previousIndex, event.currentIndex);
    } else {
      // Validación simple state-machine: no permitir retroceder desde Completed → Backlog sin pasar por intermedios
      const idxPrev = this.columns.indexOf(prevCol);
      const idxCurr = this.columns.indexOf(currCol);
      const isBackwardJump = idxCurr < idxPrev && (idxPrev - idxCurr) > 1;
      if (isBackwardJump) {
        // invalido → revert (no mover, toast silencioso)
        return;
      }
      transferArrayItem(prevList, currList, event.previousIndex, event.currentIndex);
      // TODO: PUT /api/work-items/:id/status con version → si 409, revertir move y mostrar toast ProblemDetails
    }
  }
}
