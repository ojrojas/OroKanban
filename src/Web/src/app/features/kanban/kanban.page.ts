import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CdkDrag, CdkDropList, CdkDragDrop, moveItemInArray, transferArrayItem } from '@angular/cdk/drag-drop';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';
import { WorkItemEditModalComponent } from '../../shared/ui/work-item-edit-modal/work-item-edit-modal.component';
import { KanbanBoardStore } from './kanban-board.store';

@Component({
  selector: 'app-kanban',
  standalone: true,
  imports: [CommonModule, CdkDropList, CdkDrag, FormsModule, BreadcrumbsComponent, WorkItemEditModalComponent],
  providers: [KanbanBoardStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Kanban'}]" />
    <div class="page-header" style="flex-shrink:0; display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;">
      <div>
        <h1 class="page-header__title">Kanban</h1>
        <p class="page-header__subtitle">Backlog → Planned → In Progress → Blocked → In Review → Completed</p>
      </div>
      <div style="display:flex; gap:8px; align-items:center;">
        <select [(ngModel)]="selectedProjectId" (ngModelChange)="onProjectChange()" class="input-tier1" style="min-width:220px;">
          <option value="">Select project</option>
          @for(p of projects(); track p.id){ <option [value]="p.id">{{p.name}}</option> }
        </select>
        <button class="pill" (click)="loadBoard()" [disabled]="!selectedProjectId" [title]="selectedProjectId ? 'Recargar tablero' : 'Selecciona un proyecto'">Refresh</button>
      </div>
    </div>
    @if(boardStore.error()){ <div style="background:var(--red-bg); border:1px solid var(--border); border-radius:12px; padding:8px 12px; color:var(--red-text); font-size:12px;">{{boardStore.error()}}</div> }
    @if(dirtyError){ <div style="background:var(--red-bg); border:1px solid var(--border); border-radius:12px; padding:8px 12px; color:var(--red-text); font-size:12px; margin-top:8px;">{{dirtyError}}</div> }
    @if(selectedProjectId){
      <div class="kanban-board">
       <div class="kanban-cols">
         @for (col of boardStore.columns(); track col.status) {
           <div class="kanban-col tier-2">
             <h4 class="col-title">{{col.status}} <span class="col-count">{{col.count}}</span></h4>
             <div class="col-cards"
                  cdkDropList
                  [cdkDropListData]="col.items"
                  [cdkDropListConnectedTo]="dropListIds"
                  [id]="col.status"
                  (cdkDropListDropped)="drop($event, col.status)">
                @for (card of col.items; track card.id) {
                  <div class="kanban-card" cdkDrag [cdkDragData]="card" (dblclick)="openEdit(card)" title="Double click to edit — Task debe estar cerrada para cerrar WorkItem padre">
                    <div style="display:flex; justify-content:space-between; align-items:center;">
                      <div class="card-title" style="flex:1;">{{$any(card).title}}</div>
                      <span class="badge" style="font-size:10px; background: var(--black); color:var(--on-black);">{{$any(card).type}}</span>
                    </div>
                    <div class="card-meta">{{$any(card).criticality}} • {{$any(card).priority}} @if($any(card).isOverdue){ <span style="color:var(--red-text);">Overdue</span> } @if($any(card).parentId){ • child }</div>
                    <div style="font-size:11px; color:var(--text-muted); display:flex; gap:6px; align-items:center;">
                      <span>Est {{$any(card).estimatedHours ?? 0}}h • Lab {{$any(card).actualHours ?? 0}}h</span>
                      @if($any(card).tags?.length){ <span>• {{$any(card).tags?.join(', ')}}</span> }
                    </div>
                  </div>
                }
               @if (col.items.length === 0) {
                 <div class="drop-zone">Drop here</div>
               }
             </div>
           </div>
         }
       </div>
     </div>
    } @else {
      <div class="tier-2" style="padding:32px; text-align:center; color:var(--text-muted);">Select a project to load board</div>
    }
    <!-- local fallback when no project selected but legacy cards exist -->
    @if(editItem){
      <app-work-item-edit-modal [item]="editItem" (close)="editItem=null" (saved)="onSaved($event)"></app-work-item-edit-modal>
    }
  `,
  styles: [`
    :host { display:flex; flex-direction:column; flex:1; min-height:0; }
    .input-tier1{ padding:8px 12px; border-radius:999px; border:1px solid var(--border); background:var(--flat-bg); font-size:12px; }
    .kanban-board { flex:1; min-height:0; display:flex; flex-direction:column; overflow:hidden; }
    .kanban-cols{ display:flex; gap:16px; overflow:auto; flex:1; min-height:0; padding-bottom:8px; align-items:stretch; max-height: calc(100dvh - 260px); }
    .kanban-col{ flex: 0 0 280px; min-width:280px; max-width:280px; padding:16px; display:flex; flex-direction:column; min-height:0; }
    .col-title{ margin:0 0 12px; font-size:13px; font-weight:700; display:flex; justify-content:space-between; align-items:center; }
    .col-count{ background:var(--flat-bg); border:1px solid var(--border); padding:2px 8px; border-radius:999px; font-size:11px; font-weight:500; }
    .col-cards{ flex:1; min-height:0; overflow-y:auto; overflow-x:hidden; display:flex; flex-direction:column; gap:10px; padding-right:4px; }
    .kanban-card{ background:var(--flat-bg); border:1px solid var(--border); border-radius:14px; padding:12px; font-size:13px; cursor:pointer; }
    .card-title{ font-weight:600; color:var(--text-primary); font-size:13px; }
    .card-meta{ font-size:11px; color:var(--text-muted); margin-top:4px; }
    .drop-zone{ min-height:72px; background:var(--flat-bg); border:1px dashed var(--border); border-radius:12px; display:grid; place-items:center; color:var(--text-muted); font-size:12px; margin-top:8px; }
    .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;}
  `]
})
export class KanbanPage implements OnInit {
  boardStore = inject(KanbanBoardStore);
  private http = inject(HttpClient);
  selectedProjectId = '';
  projects = signal<any[]>([]);
  editItem: any = null;
  dirty = new Map<string, boolean>();
  dirtyError: string | null = null;
  columns = ['Backlog','Planned','InProgress','Blocked','InReview','Completed'];
  get dropListIds(): string[] { return this.boardStore.columns().map(c=> c.status); }
  ngOnInit(){
    this.http.get<any>('/api/projects?page=1&pageSize=50').subscribe({ next:(res:any)=> this.projects.set(res?.items ?? res ?? []), error:()=>{}});
    // try to pick first project
    setTimeout(()=> { const list=this.projects(); if(list.length && !this.selectedProjectId){ this.selectedProjectId=list[0].id; this.onProjectChange(); }}, 800);
  }
  onProjectChange(){
    if(!this.selectedProjectId) return;
    this.boardStore.setProject(this.selectedProjectId);
    this.boardStore.loadBoard(this.selectedProjectId);
  }
  loadBoard(){
    if(!this.selectedProjectId){
      this.boardStore.setProject('');
      return;
    }
    this.boardStore.setProject(this.selectedProjectId);
    this.boardStore.loadBoard(this.selectedProjectId);
  }
  openEdit(card:any){ this.editItem = {...card}; }
  onSaved(ev:any){ this.dirty.set(ev.updated?.id ?? this.editItem.id, true); this.editItem=null; this.dirtyError=null; if(this.selectedProjectId) this.boardStore.loadBoard(this.selectedProjectId); }
  drop(event: CdkDragDrop<any>, targetCol: string): void {
    const prevCol = event.previousContainer.id;
    const currCol = event.container.id as string;
    if (event.previousContainer === event.container) {
      moveItemInArray(event.container.data, event.previousIndex, event.currentIndex);
      return;
    }
    const card:any = event.item.data;
    const isDirty = this.dirty.get(card.id);
    if(!isDirty){
      this.dirtyError = 'Debes modificar la card (double click → editar) antes de cambiar de estado. Cambio revertido.';
      setTimeout(()=> this.dirtyError=null, 4000);
      return;
    }
    // optimistic move then status API
    transferArrayItem(event.previousContainer.data, event.container.data, event.previousIndex, event.currentIndex);
    // API call via store dragDrop
    (this.boardStore as any).dragDrop({ workItemId: card.id, targetStatus: currCol, expectedVersion: card.version ?? 1 });
    // after move, reset dirty for that card until next edit
    this.dirty.set(card.id, false);
  }
}
