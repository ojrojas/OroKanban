import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { inject } from '@angular/core';

@Component({
  selector: 'app-work-item-edit-modal',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="modal-overlay" (click)="close.emit()">
      <div class="tier-2 modal" (click)="$event.stopPropagation()" style="width:min(640px,100%); max-height:90vh; overflow:auto;">
        <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
          <h3 style="margin:0; font-size:16px; font-weight:700;">Edit Card</h3>
          <button class="pill" (click)="close.emit()">✕</button>
        </div>
        @if(error){ <div style="background:var(--red-bg); border:1px solid var(--border); border-radius:12px; padding:8px 12px; color:var(--red-text); font-size:12px; margin-bottom:12px;">{{error}}</div> }
        <div style="display:flex; flex-direction:column; gap:12px;">
          <label style="font-size:12px; font-weight:600;">Title <input class="input-tier1" [(ngModel)]="form.title" /></label>
          <label style="font-size:12px; font-weight:600;">Description <textarea class="input-tier1" [(ngModel)]="form.description" rows="3"></textarea></label>
          <div style="display:flex; gap:12px;">
            <label style="flex:1; font-size:12px; font-weight:600;">Priority
              <select class="input-tier1" [(ngModel)]="form.priority">
                <option>Low</option><option>Medium</option><option>High</option><option>Critical</option><option>Urgent</option>
              </select>
            </label>
            <label style="flex:1; font-size:12px; font-weight:600;">Criticality
              <select class="input-tier1" [(ngModel)]="form.criticality">
                <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
              </select>
            </label>
          </div>
          <label style="font-size:12px; font-weight:600;">Due date <input class="input-tier1" type="date" [(ngModel)]="form.dueDate" /></label>
          <label style="font-size:12px; font-weight:600;">Deliverables (comma separated) <input class="input-tier1" [(ngModel)]="form.deliverablesStr" placeholder="Spec.pdf, Demo" /></label>
          <div style="font-size:12px; font-weight:600;">Deliverable entities (fixed enum)
            @for(d of deliverables; track d.id){
              <div style="display:flex; gap:8px; align-items:center; margin-top:6px;">
                <input class="input-tier1" style="flex:1" [(ngModel)]="d.title" placeholder="Title" />
                <select class="input-tier1" style="flex:0 0 130px" [(ngModel)]="d.type">
                  <option>Document</option><option>Artifact</option><option>Review</option><option>QA</option><option>Deployment</option><option>Evidence</option>
                </select>
                <select class="input-tier1" style="flex:0 0 110px" [(ngModel)]="d.status">
                  <option>Pending</option><option>Approved</option><option>Rejected</option>
                </select>
                <input class="input-tier1" style="flex:1" [(ngModel)]="d.url" placeholder="Url" />
                <button class="pill" (click)="removeDeliverable(d)">✕</button>
              </div>
            }
            <button class="pill" style="margin-top:8px" (click)="addDeliverable()">+ Add deliverable</button>
          </div>
          <label style="font-size:12px; font-weight:600;">Observations <textarea class="input-tier1" [(ngModel)]="form.observations" rows="3"></textarea></label>
          <label style="font-size:12px; font-weight:600;">Progress <input class="input-tier1" type="number" [(ngModel)]="form.progress" min="0" max="100" /></label>
        </div>
        <div style="display:flex; justify-content:flex-end; gap:10px; margin-top:20px;">
          <button class="btn-secondary" (click)="close.emit()">Cancel</button>
          <button class="btn-primary" (click)="save()">Save</button>
        </div>
      </div>
    </div>
  `,
  styles: [`.modal-overlay{position:fixed; inset:0; background:rgba(0,0,0,0.55); display:grid; place-items:center; z-index:1000; padding:24px;} .modal{background:var(--card-bg); border:1px solid var(--border); border-radius:24px; padding:24px 32px; box-shadow:var(--shadow-hover);} .input-tier1{width:100%; margin-top:6px; padding:10px 14px; border-radius:18px; border:1px solid var(--border); background:var(--flat-bg); font-size:13px;} .pill{padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer;} .btn-primary{background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class WorkItemEditModalComponent {
  @Input() item: any = null;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<any>();
  http = inject(HttpClient);
  error: string | null = null;
  form: any = { title:'', description:'', priority:'Medium', criticality:'Medium', dueDate:'', deliverablesStr:'', observations:'', progress:0 };
  deliverables: any[] = [];
  original: any = null;

  ngOnInit(){
    if(this.item){
      this.form = { title: this.item.title||'', description: this.item.description||'', priority: this.item.priority||'Medium', criticality: this.item.criticality||'Medium', dueDate: this.item.dueDate ? this.item.dueDate.substring(0,10):'', deliverablesStr: (this.item.deliverables||this.item.tags||[]).join(', '), observations: this.item.observations||'', progress: this.item.progress||0 };
      this.original = JSON.stringify(this.form);
      this.loadDeliverables();
    }
  }
  loadDeliverables(){
    if(!this.item?.id) return;
    this.http.get<any>(`/api/work-items/${this.item.id}/deliverables`).subscribe({
      next: (res:any)=> {
        const arr = Array.isArray(res)? res : res?.items ?? [];
        this.deliverables = arr.map((d:any)=> ({id:d.id, title:d.title, type:d.type, status:d.status, url:d.url}));
      },
      error: ()=> this.deliverables=[]
    });
  }
  addDeliverable(){ this.deliverables.push({ id: null, title:'', type:'Document', status:'Pending', url:''}); }
  removeDeliverable(d:any){ this.deliverables = this.deliverables.filter(x=> x!==d); }
  hasChanged(): boolean { return JSON.stringify(this.form) !== this.original || this.deliverables.some(d=> !d.id); }
  save(){
    if(!this.form.title.trim()){ this.error='Title required'; return; }
    const tags = this.form.deliverablesStr ? this.form.deliverablesStr.split(',').map((s:string)=> s.trim()).filter(Boolean) : [];
    // if requiring change and none, error will be handled by caller; here still save
    const payload:any = { title: this.form.title.trim(), description: this.form.description||null, priority: this.form.priority, criticality: this.form.criticality, dueDate: this.form.dueDate? new Date(this.form.dueDate).toISOString():null, tags, deliverables: tags, observations: this.form.observations||null, progress: Number(this.form.progress)||0, estimatedHours: 0 };
    this.http.put<any>(`/api/work-items/${this.item.id}`, payload).subscribe({
      next: async (updated:any) => {
        // save deliverable entities
        for(const d of this.deliverables){
          if(!d.title.trim()) continue;
          if(d.id){
            try{ await this.http.put(`/api/deliverables/${d.id}`, { title:d.title, type:d.type, status:d.status, url:d.url||null}).toPromise(); }catch{}
          } else {
            try{ const created:any = await this.http.post(`/api/work-items/${this.item.id}/deliverables`, { title:d.title, type:d.type, url:d.url||null}).toPromise(); d.id = created?.id ?? created?.data?.id; }catch{}
          }
        }
        const changed = this.hasChanged();
        this.saved.emit({ updated, changed });
      },
      error: (e:any)=> this.error = e?.error?.detail ?? 'save failed'
    });
  }
}
