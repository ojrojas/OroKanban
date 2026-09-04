import { CommonModule, Location } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, BreadcrumbsComponent],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Projects',link:'/projects'},{label: project()?.name || 'Detail'}]" />
    <div class="page-header" style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:12px;">
      <div>
        <h1 class="page-header__title">Project Detail</h1>
        <p class="page-header__subtitle">Members, timeline and history</p>
      </div>
      <div style="display:flex; gap:8px;">
        <button class="btn-secondary" (click)="goBack()">← Back to list</button>
        <button class="btn-primary" (click)="openEdit()">Edit</button>
        @if(project()?.status !== 'Archived'){
          <button class="btn-secondary" (click)="archive()" style="border-color:var(--border); color:var(--red-text);">Archive</button>
        }
      </div>
    </div>
    @if (loading()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div>
    } @else if (error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{error()}}</span><button class="btn-secondary" (click)="load()">Retry</button></div>
    } @else if (project()) {
      <div class="tier-2" style="padding:24px 32px;">
        <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">{{ project()!.name }}</h3>
        <div style="font-size:12px; color:var(--text-secondary); display:flex; gap:12px; flex-wrap:wrap;">
          <span class="badge">{{ project()!.status }}</span>
          @if(project()!.priority){ <span class="badge">{{project()!.priority}}</span> }
          @if(project()!.criticality){ <span class="badge">{{project()!.criticality}}</span> }
          @if(project()!.dueDate){ <span>Due {{ project()!.dueDate | date:'shortDate' }}</span> }
        </div>
        @if(project()!.description){ <div style="margin-top:12px; font-size:13px; color:var(--text-secondary);">{{project()!.description}}</div> }
        <div style="margin-top:8px; font-size:11px; color:var(--text-muted);">Updated {{ project()!.updatedAt | date:'short' }}</div>
      </div>
      <div class="tier-2" style="padding:24px 32px; margin-top: var(--gap-widget);">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Members ({{ members().length }})</h3>
          <button class="pill" (click)="load()">Refresh</button>
        </div>
        @for(m of members(); track m.userId){
          <div class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{m.userId}}</div><div style="font-size:12px; color:var(--text-secondary);">{{m.role}}</div></div><span class="badge">{{m.role}}</span></div>
        }
        @if(members().length===0){ <div style="font-size:12px; color:var(--text-muted);">No members</div> }
      </div>
      <div class="tier-2" style="padding:24px 32px; margin-top: var(--gap-widget);">
        <h3 style="margin:0 0 12px; font-size:14px; font-weight:700;">Timeline / History</h3>
        @if(history().length===0){ <div style="font-size:12px; color:var(--text-muted);">No history yet</div> }
        @for(h of history(); track h.id || h.timestamp){
          <div style="padding:8px 0; border-top:1px solid var(--border); font-size:12px;">
            <div style="font-weight:600;">{{h.action || h.field || 'Event'}}</div>
            <div style="color:var(--text-secondary);">{{h.detail || h.from+' → '+h.to}}</div>
            <div style="color:var(--text-muted); font-size:11px;">{{h.timestamp | date:'short'}} @if(h.actorId){ • {{h.actorId}} }</div>
          </div>
        }
      </div>
    }

    @if (showEdit) {
      <div class="modal-overlay" (click)="showEdit=false">
        <div class="tier-2 modal" (click)="$event.stopPropagation()">
          <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
            <h3 style="margin:0; font-size:16px; font-weight:700;">Edit Project</h3>
            <button class="pill" (click)="showEdit=false">✕</button>
          </div>
          <div style="display:flex; flex-direction:column; gap:12px;">
            <label style="font-size:12px; font-weight:600;">Name <input class="input-tier1" [(ngModel)]="editForm.name" /></label>
            <div style="display:flex; gap:12px;">
              <label style="flex:1; font-size:12px; font-weight:600;">Status
                <select class="input-tier1" [(ngModel)]="editForm.status">
                  <option>Draft</option><option>Active</option><option>OnHold</option><option>Completed</option><option>Archived</option>
                </select>
              </label>
              <label style="flex:1; font-size:12px; font-weight:600;">Priority
                <select class="input-tier1" [(ngModel)]="editForm.priority">
                  <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
                </select>
              </label>
              <label style="flex:1; font-size:12px; font-weight:600;">Criticality
                <select class="input-tier1" [(ngModel)]="editForm.criticality">
                  <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
                </select>
              </label>
            </div>
            <label style="font-size:12px; font-weight:600;">Due date <input class="input-tier1" type="date" [(ngModel)]="editForm.dueDate" /></label>
            <label style="font-size:12px; font-weight:600;">Description <textarea class="input-tier1" [(ngModel)]="editForm.description" rows="3"></textarea></label>
            @if(editError){ <div style="color:var(--red-text); font-size:12px;">{{editError}}</div> }
          </div>
          <div style="display:flex; justify-content:flex-end; gap:10px; margin-top:20px;">
            <button class="btn-secondary" (click)="showEdit=false">Cancel</button>
            <button class="btn-primary" (click)="saveEdit()">Save</button>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .btn-primary{background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; font-size:12px; cursor:pointer;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .modal-overlay{position:fixed; inset:0; background:rgba(0,0,0,0.55); display:grid; place-items:center; z-index:1000; padding:24px;} .modal{background:var(--card-bg); border:1px solid var(--border); border-radius:24px; padding:24px 32px; width:min(560px,100%);} .input-tier1{width:100%; margin-top:6px; padding:10px 14px; border-radius:18px; border:1px solid var(--border); background:var(--flat-bg); font-size:13px;}`]
})
export class ProjectDetailPage implements OnInit{
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private location = inject(Location);
  private http = inject(HttpClient);
  id = signal<string>('');
  project = signal<any>(null);
  members = signal<any[]>([]);
  history = signal<any[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);
  showEdit = false;
  editForm: any = { name:'', status:'Active', priority:'Medium', criticality:'Medium', dueDate:'', description:'' };
  editError: string | null = null;

   ngOnInit(){ const id = this.route.snapshot.paramMap.get('id'); if(id){ this.id.set(id); this.load(); } }
   goBack(){ this.location.back(); if(this.router.url.includes('/projects/')) setTimeout(()=>{ if(this.router.url.includes('/projects/')) this.router.navigate(['/projects']);},300); }
   load(){
     this.loading.set(true); this.error.set(null);
     this.http.get<any>(`/api/projects/${this.id()}`).subscribe({
       next: (res:any) => {
         const p = res?.data ?? res;
         this.project.set(p);
         this.members.set(p?.members ?? []);
         this.editForm = { name: p?.name||'', status: p?.status||'Active', priority: p?.priority||'Medium', criticality: p?.criticality||'Medium', dueDate: p?.dueDate ? p.dueDate.substring(0,10):'', description: p?.description||'' };
         this.loading.set(false);
         this.loadHistory();
       },
       error: (e:any) => { this.error.set(e?.error?.detail ?? e?.message ?? 'load failed'); this.loading.set(false); }
     });
   }
   loadHistory(){
     this.http.get<any>(`/api/projects/${this.id()}/history`).subscribe({
       next: (res:any)=> this.history.set(res?.items ?? res ?? []),
       error: ()=> this.history.set([])
     });
   }
   openEdit(){ this.showEdit = true; this.editError=null; }
   saveEdit(){
     const payload:any = { name: this.editForm.name.trim(), status: this.editForm.status, priority: this.editForm.priority, criticality: this.editForm.criticality, dueDate: this.editForm.dueDate ? new Date(this.editForm.dueDate).toISOString():null, description: this.editForm.description||null };
     if(payload.name.length<3) { this.editError='Name 3..200'; return; }
     this.http.put<any>(`/api/projects/${this.id()}`, payload).subscribe({
       next: ()=> { this.showEdit=false; this.load(); },
       error: (e:any)=> this.editError = e?.error?.detail ?? 'save failed'
     });
   }
   archive(){
     if(!confirm('Archive project?')) return;
     this.http.post<any>(`/api/projects/${this.id()}/archive`, {}).subscribe({
       next: ()=> this.load(),
       error: (e:any)=> alert(e?.error?.detail ?? 'archive failed')
     });
   }
}
