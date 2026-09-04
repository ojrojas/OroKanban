import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { TeamTasksStore } from './team-tasks.store';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-team-tasks',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, BreadcrumbsComponent],
  providers: [TeamTasksStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Team Tasks'}]" />
     <div class="page-header" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;">
      <div>
        <h1 class="page-header__title">Team Tasks</h1>
        <p class="page-header__subtitle">Subtree tasks for your team — WorkItems (Feature/Plan/Issue) → Tasks en Kanban</p>
      </div>
      <div style="display:flex; gap:8px;">
        <button class="btn-primary" (click)="openCreate('Issue')">+ New WorkItem</button>
        <button class="btn-primary" (click)="openCreate('Task')">+ New Task</button>
      </div>
    </div>
    <div class="toolbar" style="flex-wrap:wrap;">
      <div class="search-bar tier-1" style="flex:1; max-width:320px;">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
        <input placeholder="Search team tasks..." [value]="store.q()" (input)="store.setQ($any($event.target).value)" style="flex:1; border:none; outline:none; background:transparent; font-size:13px;" />
      </div>
      <div class="filter-pills">
        <button class="pill" [class.active]="store.filter()==='all'" (click)="store.setFilter('all')">All</button>
        <button class="pill" [class.active]="store.filter()==='Blocked'" (click)="store.setFilter('Blocked')">Blocked</button>
        <button class="pill" [class.active]="store.filter()==='Overdue'" (click)="store.setFilter('Overdue')">Overdue</button>
      </div>
      <select [value]="pageSize()" (change)="setPageSize($any($event.target).value)" style="padding:6px 10px; border-radius:999px; border:1px solid var(--border); background:var(--flat-bg); font-size:12px;">
        <option value="10">10</option><option value="15">15</option><option value="25">25</option><option value="50">50</option>
      </select>
      <button class="pill" (click)="store.load()" title="Recargar">Refresh</button>
    </div>
    @if(showCreate()){
      <div class="modal-overlay" (click)="showCreate.set(false)">
        <div class="tier-2 modal" (click)="$event.stopPropagation()">
          <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">{{ newForm.type === 'Task' || newForm.type === 'Subtask' ? 'New Task' : 'New WorkItem' }} <span style="font-size:11px; color:var(--text-muted);">{{ newForm.type }}</span></h3>
          @if(createError()){ <div style="color:var(--red-text); font-size:12px; margin-bottom:8px;">{{createError()}}</div> }
          <div style="display:flex; flex-direction:column; gap:10px;">
            <label style="font-size:12px; font-weight:600;">Project
              <select class="input-tier1" [(ngModel)]="newForm.projectId" (ngModelChange)="onProjectChange()">
                <option value="">Select project</option>
                @for(p of projects(); track p.id){ <option [value]="p.id">{{p.name}}</option> }
              </select>
            </label>
            <label style="font-size:12px; font-weight:600;">Tipo de WorkItem
              <select class="input-tier1" [(ngModel)]="newForm.type">
                <option value="Feature">Feature</option>
                <option value="Plan">Plan</option>
                <option value="Issue">Issue</option>
                <option value="Task">Task</option>
                <option value="Subtask">Subtask</option>
              </select>
            </label>
            <div style="font-size:11px; color:var(--text-muted);">
              @if(newForm.type==='Feature' || newForm.type==='Plan' || newForm.type==='Issue'){
                <span>WorkItem que se <b>asigna al subordinado</b> para que lo resuelva. El subordinado creará Tasks hijas para cerrarlo. Solo el <b>creador</b> puede editar el WorkItem.</span>
              } @else {
                <span>Task que <b>debe colgar de un WorkItem</b> (Feature/Plan/Issue). Se gestiona en Kanban; debe estar <b>Completed</b> para que el WorkItem padre pueda cerrarse.</span>
              }
            </div>
            @if(newForm.type==='Task' || newForm.type==='Subtask'){
              <label style="font-size:12px; font-weight:600;">WorkItem padre * (requerido para Task/Subtask)
                <select class="input-tier1" [(ngModel)]="newForm.parentId">
                  <option value="">— Selecciona WorkItem padre (Feature/Plan/Issue) —</option>
                  @for(w of parentWorkItems(); track w.id){ <option [value]="w.id">{{w.title}} ({{w.type}} • {{w.status}})</option> }
                </select>
              </label>
            } @else {
              <label style="font-size:12px; font-weight:600;">WorkItem padre (opcional)
                <select class="input-tier1" [(ngModel)]="newForm.parentId">
                  <option value="">— Sin padre (WorkItem raíz) —</option>
                  @for(w of parentWorkItems(); track w.id){ <option [value]="w.id">{{w.title}} ({{w.type}} • {{w.status}})</option> }
                </select>
              </label>
            }
            <label style="font-size:12px; font-weight:600;">Title <input class="input-tier1" [(ngModel)]="newForm.title" /></label>
            <label style="font-size:12px; font-weight:600;">Description <textarea class="input-tier1" [(ngModel)]="newForm.description" rows="2"></textarea></label>
            <div style="display:flex; gap:10px;">
              <label style="flex:1; font-size:12px; font-weight:600;">Priority <select class="input-tier1" [(ngModel)]="newForm.priority"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option><option>Urgent</option></select></label>
              <label style="flex:1; font-size:12px; font-weight:600;">Criticality <select class="input-tier1" [(ngModel)]="newForm.criticality"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select></label>
            </div>
            <label style="font-size:12px; font-weight:600;">Tiempo estimado (horas sugeridas por el asignador) <input class="input-tier1" type="number" min="0" step="0.5" [(ngModel)]="newForm.estimatedHours" placeholder="Ej: 8" /></label>
            <div style="font-size:11px; color:var(--text-muted);">El empleado registrará luego el <b>tiempo real</b> en el detalle de la Task; el sistema compara estimado vs real para productividad.</div>
            <label style="font-size:12px; font-weight:600;">Deliverables (comma) <input class="input-tier1" [(ngModel)]="newForm.deliverables" placeholder="Spec, Demo" /></label>
            <div style="font-size:11px; color:var(--text-muted);">Se guardarán como entregables; los tags se generan automáticamente en minúsculas <code>a-z0-9_-</code>. Evita puntos/espacios: se normalizan a guiones.</div>
            <label style="font-size:12px; font-weight:600;">Deliverable entity type <select class="input-tier1" [(ngModel)]="newForm.deliverableType"><option>Document</option><option>Artifact</option><option>Review</option><option>QA</option><option>Deployment</option><option>Evidence</option></select></label>
            <label style="font-size:12px; font-weight:600;">Assignee (subordinado) <select class="input-tier1" [(ngModel)]="newForm.assignee"><option value="">Unassigned</option><option value="01a06801-1345-7b00-a052-83b7be137228">Operator1 Operator1 (Contributor)</option><option value="01a06801-a457-7323-8316-40726313b076">Operator2 Operator2 (Contributor)</option><option value="01a067ff-bab8-7529-aff7-c6ce7fb7363c">Manager1 Manager1 (Manager)</option><option value="01a06800-5db7-753b-a61b-7876ca7b5828">Manager2 Manager2 (Manager)</option></select></label>
            <div style="font-size:11px; color:var(--text-muted);">Se asigna a un subordinado de tu subtree (Organization). El manager vela por el cumplimiento.</div>
            <label style="font-size:12px; font-weight:600;">Due date (fecha de entrega del WorkItem/Task) <input class="input-tier1" type="date" [(ngModel)]="newForm.dueDate" /></label>
          </div>
          <div style="display:flex; justify-content:flex-end; gap:10px; margin-top:16px;">
            <button class="btn-secondary" (click)="showCreate.set(false)">Cancel</button>
            <button class="btn-primary" (click)="create()">Create</button>
          </div>
        </div>
      </div>
    }
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; border-bottom:1px solid var(--border); display:flex; justify-content:space-between;"><h3 style="margin:0; font-size:14px; font-weight:700;">Team Tasks</h3><span class="badge">{{store.count()}} total</span></div>
        @for (t of store.filtered(); track t.id) {
          <a [routerLink]="['/work-items', t.id]" class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{t.title ?? t.name}}</div><div style="font-size:12px; color:var(--text-secondary);">{{t.status}}</div></div><span class="badge">{{t.status}}</span></a>
        }
        @if (store.filtered().length===0) { <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No team tasks</div> }
      </div>
    }
  `,
  styles: [`.toolbar{display:flex; gap:12px; margin-bottom:var(--gap-widget); align-items:center; flex-wrap:wrap;} .search-bar{display:flex; gap:10px; padding:10px 16px; border-radius:18px; background:var(--flat-bg); border:1px solid var(--border); align-items:center;} .search-bar input{flex:1; border:none; outline:none; background:transparent; font-size:13px; color:var(--text-primary);} .search-bar input::placeholder{color:var(--text-muted);} .filter-pills{display:inline-flex; background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:2px; gap:2px;} .pill{padding:6px 14px; border-radius:999px; font-size:12px; background:transparent; border:none; color:var(--text-secondary); cursor:pointer; font-weight:500; transition:all 150ms ease;} .pill:hover:not(.active){background:var(--border); color:var(--text-primary);} .pill.active{background:var(--black); color:var(--on-black); box-shadow:var(--shadow-card);} .row{display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); text-decoration:none; align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;} .btn-primary{background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:6px 12px; font-size:12px; cursor:pointer;} .modal-overlay{position:fixed; inset:0; background:rgba(0,0,0,0.5); display:grid; place-items:center; z-index:1000; padding:24px;} .modal{background:var(--card-bg); border:1px solid var(--border); border-radius:24px; padding:24px; width:min(560px,100%);} .input-tier1{width:100%; margin-top:6px; padding:10px 14px; border-radius:18px; border:1px solid var(--border); background:var(--flat-bg); font-size:13px; color:var(--text-primary);}`]
})
export class TeamTasksPage implements OnInit {
  store = inject(TeamTasksStore);
  private http = inject(HttpClient);
  showCreate = signal(false);
  createError = signal<string|null>(null);
  projects = signal<any[]>([]);
  parentWorkItems = signal<any[]>([]);
  pageSize = signal(15);
  newForm:any={ projectId:'', parentId:'', type:'Issue', title:'', description:'', priority:'Medium', criticality:'Medium', deliverables:'', deliverableType:'Document', assignee:'', dueDate:'', estimatedHours: 4 };
  ngOnInit(): void { this.store.load(); this.http.get<any>('/api/projects?page=1&pageSize=50').subscribe({ next:(r:any)=> this.projects.set(r?.items ?? r ?? []), error:()=>{} }); }
  openCreate(type: string){
    this.newForm.type = type;
    if(type==='Task' || type==='Subtask'){
      // Task requiere padre
      if(!this.newForm.projectId && this.projects().length){ this.newForm.projectId = this.projects()[0].id; this.onProjectChange(); }
    }
    this.showCreate.set(true);
  }
  onProjectChange(){
    const pid = this.newForm.projectId;
    if(!pid){ this.parentWorkItems.set([]); return; }
    this.http.get<any>(`/api/work-items?projectId=${pid}&pageSize=50`).subscribe({ next:(r:any)=>{
      const items = r?.items ?? r ?? [];
      // Padres válidos: Feature/Plan/Issue/Epic (WorkItems), no Tasks sueltos como padre de Feature
      const parents = (Array.isArray(items)?items:[]).filter((x:any)=> ['Feature','Plan','Issue','Epic','Feature','Plan'].includes(x.type) || x.Type);
      // Fallback: si no hay padres de esos tipos, muestra todos para no bloquear
      this.parentWorkItems.set(parents.length ? parents : (Array.isArray(items)?items:[]));
    }, error:()=> this.parentWorkItems.set([])});
  }
  setPageSize(v:string){ const n=parseInt(v,10); this.pageSize.set(n); this.store.load(); }
  private sanitizeTag(t:string):string {
    return t.trim().toLowerCase().replace(/[^a-z0-9_-]+/g,'-').replace(/-+/g,'-').replace(/^-|-$/g,'').slice(0,50);
  }
  create(){
    if(!this.newForm.projectId || !this.newForm.title.trim()){ this.createError.set('Project and title required'); return; }
    if((this.newForm.type==='Task' || this.newForm.type==='Subtask') && !this.newForm.parentId){
      this.createError.set('Task/Subtask debe tener un WorkItem padre (Feature/Plan/Issue)');
      return;
    }
    const rawDeliverables = this.newForm.deliverables ? this.newForm.deliverables.split(',').map((s:string)=>s.trim()).filter(Boolean):[];
    const tags = rawDeliverables.map((s:string)=>this.sanitizeTag(s)).filter((s:string)=> s.length>=1).slice(0,10);
    const responsibleId = this.newForm.assignee || null;
    const parentId = this.newForm.parentId || null;
    const payload:any={ title:this.newForm.title.trim(), description:this.newForm.description||null, type: this.newForm.type, priority:this.newForm.priority, criticality:this.newForm.criticality, dueDate: this.newForm.dueDate? new Date(this.newForm.dueDate).toISOString():null, tags, deliverables: rawDeliverables, estimatedHours: Number(this.newForm.estimatedHours)||0, progress:0, responsibleId, parentId };
    // Ensure responsibleId is sent as Guid if provided
    if(responsibleId) payload.responsibleId = responsibleId;
    this.http.post(`/api/projects/${this.newForm.projectId}/work-items`, payload).subscribe({
      next: (created:any)=>{
        const id = created?.id ?? created?.data?.id;
        // create deliverable entities (raw names, enum fixed)
        if(rawDeliverables.length && id){
          for(const t of rawDeliverables){ this.http.post(`/api/work-items/${id}/deliverables`, { title:t, type:this.newForm.deliverableType, url:null}).subscribe({ error:()=>{} }); }
        }
        this.showCreate.set(false); this.store.load();
      },
      error:(e:any)=> this.createError.set(e?.error?.detail ?? e?.error?.title ?? 'create failed')
    });
  }
}
