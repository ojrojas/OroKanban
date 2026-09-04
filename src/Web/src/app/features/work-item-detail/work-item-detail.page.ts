import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { WorkItemDetailStore } from './work-item-detail.store';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';
import { WorkItemEditModalComponent } from '../../shared/ui/work-item-edit-modal/work-item-edit-modal.component';

@Component({
  selector: 'app-work-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, BreadcrumbsComponent, WorkItemEditModalComponent],
  providers: [WorkItemDetailStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Work Items',link:'/my-tasks'},{label: store.item()?.title || 'Detail'}]" />
    <div class="page-header" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;">
      <div>
        <h1 class="page-header__title">Work Item Detail</h1>
        <p class="page-header__subtitle">Progress with explanation link</p>
      </div>
      <div style="display:flex; gap:8px;">
        <button class="btn-secondary" (click)="goBack()">← Back</button>
        <button class="btn-primary" (click)="editOpen=true">Edit</button>
      </div>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="load()">Retry</button></div> }
     @else if (store.item()) {
       <div class="tier-2" style="padding:24px 32px;">
         <div style="display:flex; justify-content:space-between; align-items:center;">
           <h3 style="margin:0; font-size:14px; font-weight:700;">{{$any(store.item())?.title ?? 'Work Item'}}</h3>
           <span class="badge">{{$any(store.item())?.status ?? 'In Progress'}}</span>
         </div>
          <div style="margin-top:8px; display:flex; gap:8px; flex-wrap:wrap;">
            <span class="badge" style="background: var(--black); color:var(--on-black);">{{$any(store.item())?.type}}</span>
            <span class="badge">{{$any(store.item())?.priority}}</span>
            <span class="badge">{{$any(store.item())?.criticality}}</span>
            @if($any(store.item())?.dueDate){ <span class="badge">Due {{$any(store.item()).dueDate | date:'shortDate'}}</span> }
            <span class="badge">Est {{$any(store.item())?.estimatedHours ?? 0}}h • Real {{$any(store.item())?.actualHours ?? 0}}h</span>
            <span class="badge">v{{$any(store.item())?.version}} @if($any(store.item())?.reopenedCount){ • Reabierto {{$any(store.item()).reopenedCount}}× }</span>
          </div>
          <div style="margin-top:12px; font-size:13px; color:var(--text-secondary);">{{$any(store.item())?.description ?? 'No description'}}</div>
          @if($any(store.item())?.observations){ <div style="margin-top:12px; font-size:13px; padding:12px; background:var(--flat-bg); border:1px solid var(--border); border-radius:12px;"><strong>Observations:</strong> {{$any(store.item()).observations}}</div> }
          <div style="margin-top:12px; padding:10px 12px; background: var(--flat-bg); border:1px solid var(--border); border-radius:12px; display:flex; gap:8px; align-items:center; flex-wrap:wrap;">
            <span style="font-size:12px; font-weight:600;">Registrar tiempo real (horas laboradas):</span>
            <input type="number" min="0" step="0.5" placeholder="Ej: 2.5" [(ngModel)]="actualHoursInput" style="width:100px; padding:6px 10px; border-radius:999px; border:1px solid var(--border);" />
            <button class="pill" (click)="registerTime()">Guardar horas</button>
            <span style="font-size:11px; color:var(--text-muted);">Se compara vs estimado {{ $any(store.item())?.estimatedHours ?? 0 }}h para efectividad. El reloj del API acumula al cambiar estado (no manipulable).</span>
          </div>
          <div style="margin-top:12px; font-size:12px; padding:10px 12px; background:var(--flat-bg); border:1px solid var(--border); border-radius:12px;">
            <strong>Asignado a:</strong>
            @if($any(store.item())?.responsibleId){
              <span style="font-weight:600;">{{ resolveUser($any(store.item()).responsibleId).name }}</span>
              <span class="badge" style="margin-left:6px;">{{ resolveUser($any(store.item()).responsibleId).role }}</span>
              <span style="font-size:11px; color:var(--text-muted); margin-left:6px;">{{ $any(store.item()).responsibleId }}</span>
            } @else {
              <span style="color:var(--text-muted);">Sin asignar</span>
            }
            @if($any(store.item())?.ownerId){ <div style="font-size:11px; color:var(--text-muted); margin-top:4px;">Owner: {{ resolveUser($any(store.item()).ownerId).name }} • Reviewer: {{ $any(store.item()).reviewerId ? resolveUser($any(store.item()).reviewerId).name : '—' }}</div> }
          </div>
          <div style="margin-top:12px; font-size:12px;">
            <strong>Tags / Deliverables:</strong> {{$any(store.item())?.tags?.join(', ') || '—'}} @if($any(store.item())?.deliverables?.length){ • {{$any(store.item()).deliverables.join(', ')}} }
          </div>
         @if(deliverables.length){
           <div style="margin-top:12px;">
             <h4 style="font-size:12px; font-weight:700;">Deliverable entities</h4>
             @for(d of deliverables; track d.id){ <div style="font-size:12px; padding:6px 0; border-top:1px solid var(--border);">{{d.title}} — {{d.type}} — {{d.status}} @if(d.url){ <a [href]="d.url" target="_blank" style="color:var(--text-secondary)">{{d.url}}</a> } </div> }
           </div>
         }
         <div style="margin-top:16px; padding:12px; background:var(--flat-bg); border:1px solid var(--border); border-radius:12px;">
           <div style="display:flex; justify-content:space-between; align-items:center;">
             <span style="font-weight:600; font-size:13px;">Progress {{$any(store.item())?.progress ?? 0}}%</span>
             <button class="pill" (click)="showExplain.set(!showExplain())">Why?</button>
           </div>
           @if (showExplain()) {
             <div style="margin-top:12px; font-size:12px; color:var(--text-secondary);">
               <div>{{ store.progressExplanation()?.breakdown ?? 'subtasks 2/3, evidence approved, metric X' }}</div>
             </div>
           }
         </div>
          <div style="margin-top:16px; border-top:1px solid var(--border); padding-top:16px;">
            <div style="display:flex; justify-content:space-between; align-items:center;">
              <h4 style="font-size:12px; font-weight:700;">Tareas hijas ({{children.length}}) — WorkItem → Tasks</h4>
              <button class="pill" (click)="showAddTask=!showAddTask">{{ showAddTask ? 'Cancelar' : '+ Add Task' }}</button>
            </div>
            @if(showAddTask){
              <div style="margin-top:10px; padding:12px; background:var(--flat-bg); border:1px solid var(--border); border-radius:12px; display:flex; flex-direction:column; gap:8px;">
                <input class="input-tier1" placeholder="Título de la tarea" [(ngModel)]="newTask.title" style="padding:8px 12px; border-radius:999px; border:1px solid var(--border);" />
                <div style="display:flex; gap:8px; flex-wrap:wrap;">
                  <select [(ngModel)]="newTask.priority" style="flex:1; min-width:120px; padding:6px 10px; border-radius:999px; border:1px solid var(--border);"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select>
                  <select [(ngModel)]="newTask.criticality" style="flex:1; min-width:120px; padding:6px 10px; border-radius:999px; border:1px solid var(--border);"><option>Low</option><option>Medium</option><option>High</option><option>Critical</option></select>
                  <select [(ngModel)]="newTask.assignee" style="flex:1; min-width:140px; padding:6px 10px; border-radius:999px; border:1px solid var(--border);"><option value="">Sin asignar</option><option value="01a06801-1345-7b00-a052-83b7be137228">Operator1</option><option value="01a06801-a457-7323-8316-40726313b076">Operator2</option></select>
                  <input type="number" min="0" step="0.5" placeholder="Est. h" [(ngModel)]="newTask.estimatedHours" style="width:90px; padding:6px 10px; border-radius:999px; border:1px solid var(--border);" />
                </div>
                <input class="input-tier1" placeholder="Entregables coma (se normaliza tag)" [(ngModel)]="newTask.deliverables" style="padding:8px 12px; border-radius:999px; border:1px solid var(--border);" />
                <button class="btn-primary" (click)="createChildTask()" style="align-self:flex-end; padding:6px 14px; border-radius:999px; background:var(--black); color:var(--on-black); border:none; font-size:12px;">Crear Task</button>
              </div>
            }
            @for(c of children; track c.id){ 
              <a [routerLink]="['/work-items', c.id]" style="display:flex; gap:10px; padding:10px 0; border-top:1px solid var(--border); text-decoration:none; color:inherit; align-items:center;">
                <div style="width:28px; height:28px; border-radius:50%; background:var(--border); display:grid; place-items:center; font-size:10px; font-weight:700;">{{c.title.slice(0,2).toUpperCase()}}</div>
                <div style="flex:1"><div style="font-weight:600; font-size:13px;">{{c.title}}</div><div style="font-size:11px; color:var(--text-secondary);">{{c.status}} • {{c.priority}}/{{c.criticality}} @if(c.responsibleId){ • {{resolveUser(c.responsibleId).name}} }</div></div>
                <span class="badge">{{c.status}}</span>
              </a>
            }
            @if(children.length===0){ <div style="font-size:12px; color:var(--text-muted); padding:8px 0;">Este WorkItem aún no tiene Tasks hijas. Crea una con "Add Task".</div> }
            <div style="font-size:11px; color:var(--text-muted); margin-top:6px;">Cada Task hija también tiene su <i>historial de cambios</i> propio — entra a su detalle para verlo.</div>
          </div>

          <div style="margin-top:16px;">
            <h4 style="font-size:12px; font-weight:700;">History — {{ $any(store.item())?.title?.slice(0,20) }} ({{history.length}} cambios, incluye tags/deliverables granular)</h4>
            @for(h of history; track h.id){ <div style="padding:8px 0; border-top:1px solid var(--border); font-size:12px;"><div style="font-weight:600;">{{h.field}} @if(h.field==='Deliverables'){ <span style="font-size:10px; background:var(--flat-bg); border:1px solid var(--border); padding:2px 6px; border-radius:999px;">diff granular</span> }</div><div style="color:var(--text-secondary); word-break:break-all; font-size:11px;">{{h.fromJson}} → {{h.toJson}}</div><div style="color:var(--text-muted); font-size:11px;">{{h.createdAt | date:'short'}} @if(h.actorId){ • {{resolveUser(h.actorId).name}} } @if(h.comment){ • {{h.comment}} }</div></div> }
            @if(history.length===0){ <div style="font-size:12px; color:var(--text-muted);">No history yet — edita este WorkItem o sus Tasks para generar historial (tags/deliverables/status).</div> }
          </div>
       </div>
     } @else {
       <div class="tier-2" style="padding:32px; text-align:center; color:var(--text-muted);">Select a work item</div>
     }
     @if(editOpen && store.item()){ <app-work-item-edit-modal [item]="store.item()" (close)="editOpen=false" (saved)="onSaved()"></app-work-item-edit-modal> }
   `,
  styles: [`.badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;} .btn-primary{background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer;}`]
})
export class WorkItemDetailPage implements OnInit {
  store = inject(WorkItemDetailStore);
  showExplain = signal(false);
  private route = inject(ActivatedRoute);
  private http = inject(HttpClient);
  private location = inject(Location);
  editOpen = false;
  history:any[]=[];
  deliverables:any[]=[];
  children:any[]=[];
  showAddTask = false;
  newTask:any = { title:'', priority:'Medium', criticality:'Medium', assignee:'', deliverables:'', estimatedHours: 4 };
  actualHoursInput: number | null = null;
  // Synthetic user map (IdentityServer seeded) — same as organization synthetic
  private userMap = new Map<string, {name:string, role:string}>([
    ["01a065aa-9020-70a9-a1e0-b844196713c7", {name:"Admin Administrator", role:"Administrator"}],
    ["01a067ff-bab8-7529-aff7-c6ce7fb7363c", {name:"Manager1 Manager1", role:"Manager"}],
    ["01a06800-5db7-753b-a61b-7876ca7b5828", {name:"Manager2 Manager2", role:"Manager"}],
    ["01a06801-1345-7b00-a052-83b7be137228", {name:"Operator1 Operator1", role:"Contributor"}],
    ["01a06801-a457-7323-8316-40726313b076", {name:"Operator2 Operator2", role:"Contributor"}],
  ]);
  private sanitizeTag(t:string):string { return t.trim().toLowerCase().replace(/[^a-z0-9_-]+/g,'-').replace(/-+/g,'-').replace(/^-|-$/g,'').slice(0,50); }
  resolveUser(id:string): {name:string, role:string} {
    if(!id) return {name:"—", role:"—"};
    const v = this.userMap.get(String(id).toLowerCase());
    if(v) return v;
    return {name: id.slice(0,8)+"…", role:"Member"};
  }
  ngOnInit(): void { this.load(); }
  goBack(){ this.location.back(); }
  load(): void {
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    if (id) {
      (this.store as any).load(id);
      this.http.get<any>(`/api/work-items/${id}/history`).subscribe({ next:(r:any)=> this.history = r?.items ?? r ?? [], error:()=>{}});
      this.http.get<any>(`/api/work-items/${id}/deliverables`).subscribe({ next:(r:any)=> this.deliverables = Array.isArray(r)? r : r?.items ?? [], error:()=>{}});
      // Load children Tasks for this WorkItem (parent) via dedicated endpoint
      this.http.get<any>(`/api/work-items/${id}/children`).subscribe({ next:(r:any)=>{
        const items = Array.isArray(r) ? r : (r?.items ?? []);
        this.children = Array.isArray(items) ? items : [];
        // Fallback: if endpoint returns empty, try generic list filtered client-side
        if(this.children.length===0){
          const proj = (this.store.item() as any)?.projectId;
          if(proj){
            this.http.get<any>(`/api/work-items?projectId=${proj}&pageSize=100`).subscribe({ next:(r2:any)=>{
              const items2 = r2?.items ?? r2 ?? [];
              const filtered = (Array.isArray(items2)?items2:[]).filter((x:any)=> String(x.parentId ?? x.ParentId ?? '').toLowerCase() === String(id).toLowerCase());
              if(filtered.length) this.children = filtered;
            }});
          }
        }
      }, error:()=>{
        const proj = (this.store.item() as any)?.projectId;
        if(proj){
          this.http.get<any>(`/api/work-items?projectId=${proj}&pageSize=100`).subscribe({ next:(r:any)=>{
            const items = r?.items ?? r ?? [];
            this.children = (Array.isArray(items)?items:[]).filter((x:any)=> String(x.parentId ?? x.ParentId ?? '').toLowerCase() === String(id).toLowerCase());
          }});
        }
      }});
    }
  }
  onSaved(){ this.editOpen=false; this.load(); }
  registerTime(){
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    if(!id || this.actualHoursInput==null) return;
    this.http.post(`/api/work-items/${id}/time`, { actualHours: Number(this.actualHoursInput), comment: 'Registro manual horas sugeridas vs real' }).subscribe({
      next: ()=> { this.actualHoursInput=null; this.load(); },
      error:(e:any)=> alert(e?.error?.detail ?? 'registro horas falló')
    });
  }
  createChildTask(){
    const id = this.route.snapshot.paramMap.get('id') ?? '';
    const proj = (this.store.item() as any)?.projectId;
    if(!id || !proj || !this.newTask.title.trim()) return;
    const rawDelivs = this.newTask.deliverables ? this.newTask.deliverables.split(',').map((s:string)=>s.trim()).filter(Boolean):[];
    const tags = rawDelivs.map((s:string)=>this.sanitizeTag(s)).filter((s:string)=>s.length>=1).slice(0,10);
    const payload:any = {
      title: this.newTask.title.trim(),
      description: null,
      type: 'Task',
      priority: this.newTask.priority,
      criticality: this.newTask.criticality,
      parentId: id,
      responsibleId: this.newTask.assignee || null,
      tags, deliverables: rawDelivs, estimatedHours: Number(this.newTask.estimatedHours)||0, progress:0
    };
    this.http.post(`/api/projects/${proj}/work-items`, payload).subscribe({
      next: (created:any)=>{
        const nid = created?.id ?? created?.data?.id;
        if(rawDelivs.length && nid){
          for(const t of rawDelivs){ this.http.post(`/api/work-items/${nid}/deliverables`, {title:t, type:'Document', url:null}).subscribe({error:()=>{}}); }
        }
        this.showAddTask=false; this.newTask={title:'', priority:'Medium', criticality:'Medium', assignee:'', deliverables:''};
        this.load();
      },
      error:(e:any)=> alert(e?.error?.detail ?? 'create task failed: Tag must match ^[a-z0-9_-]+$ — usa minúsculas y guiones')
    });
  }
}
