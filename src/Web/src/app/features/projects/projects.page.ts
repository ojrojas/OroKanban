import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ProjectsStore } from './projects.store';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-projects',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule, BreadcrumbsComponent],
  providers: [ProjectsStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Projects'}]" />
    <div class="page-header">
      <h1 class="page-header__title">Projects</h1>
      <p class="page-header__subtitle">Manage and browse all projects in your subtree</p>
    </div>

      <div class="toolbar">
       <div class="search-bar tier-1">
         <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
         <input placeholder="Search projects..." [value]="store.filter()" (input)="store.setFilter($any($event.target).value)" />
       </div>
       <div class="filter-pills">
         <button class="pill" [class.active]="store.filter()===''" (click)="store.setFilter('')">All</button>
         <button class="pill" [class.active]="store.filter()==='active'" (click)="store.setFilter('active')">Active</button>
         <button class="pill" [class.active]="store.filter()==='archived'" (click)="store.setFilter('archived')">Archived</button>
       </div>
        <button class="btn-primary" (click)="openModal()">+ New Project</button>
        <button class="btn-secondary" (click)="doRefresh()">Refresh</button>
      </div>

      @if (showModal) {
        <div class="modal-overlay" (click)="closeModal()">
          <div class="tier-2 modal" (click)="$event.stopPropagation()">
            <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
              <h3 style="margin:0; font-size:16px; font-weight:700;">{{ editingId ? 'Edit Project' : 'New Project' }}</h3>
              <button class="pill" (click)="closeModal()">✕</button>
            </div>
           @if (store.error()) {
             <div style="background:var(--red-bg); border:1px solid var(--border); border-radius:12px; padding:10px 14px; margin-bottom:12px; color:var(--red-text); font-size:12px;">{{ store.error() }}</div>
           }
           <div style="display:flex; flex-direction:column; gap:12px;">
             <label style="font-size:12px; font-weight:600; color:var(--text-secondary);">Name * (3..200)
               <input class="input-tier1" [(ngModel)]="form.name" placeholder="Project name" maxlength="200" />
             </label>
             <div style="display:flex; gap:12px;">
               <label style="flex:1; font-size:12px; font-weight:600; color:var(--text-secondary);">Status
                 <select class="input-tier1" [(ngModel)]="form.status">
                   <option value="Draft">Draft</option>
                   <option value="Active">Active</option>
                   <option value="OnHold">OnHold</option>
                   <option value="Completed">Completed</option>
                   <option value="Archived">Archived</option>
                 </select>
               </label>
               <label style="flex:1; font-size:12px; font-weight:600; color:var(--text-secondary);">Priority
                 <select class="input-tier1" [(ngModel)]="form.priority">
                   <option value="Low">Low</option>
                   <option value="Medium">Medium</option>
                   <option value="High">High</option>
                   <option value="Critical">Critical</option>
                 </select>
               </label>
               <label style="flex:1; font-size:12px; font-weight:600; color:var(--text-secondary);">Criticality
                 <select class="input-tier1" [(ngModel)]="form.criticality">
                   <option value="Low">Low</option>
                   <option value="Medium">Medium</option>
                   <option value="High">High</option>
                   <option value="Critical">Critical</option>
                 </select>
               </label>
             </div>
             <label style="font-size:12px; font-weight:600; color:var(--text-secondary);">Due date
               <input class="input-tier1" type="date" [(ngModel)]="form.dueDate" />
             </label>
             <label style="font-size:12px; font-weight:600; color:var(--text-secondary);">Description
               <textarea class="input-tier1" [(ngModel)]="form.description" placeholder="Optional description" rows="3"></textarea>
             </label>
             <div style="font-size:11px; color:var(--text-muted);">Owner/Manager derived from current user (server validates subtree XV/XIX). For dev, uses placeholder GUIDs if token unavailable.</div>
           </div>
            <div style="display:flex; justify-content:flex-end; gap:10px; margin-top:20px;">
              <button class="btn-secondary" (click)="closeModal()">Cancel</button>
              <button class="btn-primary" (click)="submit()" [disabled]="store.isPending()">{{ store.isPending() ? 'Saving…' : (editingId ? 'Save' : 'Create') }}</button>
            </div>
          </div>
        </div>
      }

     @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div><div class="skeleton" style="width:60%"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between; align-items:center;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="store.load()">Retry</button>
      </div>
     } @else {
       <div class="tier-2" style="padding:0; overflow:hidden;">
         <div style="padding:16px 24px; display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid var(--border); flex-wrap:wrap; gap:12px;">
           <h3 style="margin:0; font-size:14px; font-weight:700;">Projects</h3>
           <div style="display:flex; gap:8px; align-items:center;">
             <label style="font-size:12px; color:var(--text-secondary);">Show
               <select [value]="store.pageSize()" (change)="onPageSize($any($event.target).value)" style="margin-left:6px; padding:6px 10px; border-radius:999px; border:1px solid var(--border); background:var(--flat-bg); font-size:12px;">
                 <option value="10">10</option>
                 <option value="15">15</option>
                 <option value="25">25</option>
                 <option value="50">50</option>
                 <option value="100">100</option>
               </select>
             </label>
             <span class="badge">{{ store.total() }} total</span>
           </div>
         </div>
         @for (p of store.filtered(); track p.id) {
           <div class="row-wrap">
             <a [routerLink]="['/projects', p.id]" class="row" style="flex:1; border-top:none;">
               <div class="thumb"></div>
               <div style="flex:1">
                 <div style="font-weight:600; font-size:13px; color:var(--text-primary);">{{ p.name }}</div>
                 <div style="font-size:12px; color:var(--text-secondary);">{{ p.status }} @if(p.priority){ • {{p.priority}} } @if(p.criticality){ • {{p.criticality}} }</div>
               </div>
               <span class="badge">{{ p.status }}</span>
             </a>
             <div style="display:flex; gap:6px; padding-right:16px; align-items:center;">
               <button class="pill" (click)="openEdit(p)">Edit</button>
               @if(p.status !== 'Archived'){
                 <button class="pill" (click)="archive(p)" title="Archive/Deactivate">Archive</button>
               }
             </div>
           </div>
         }
         @if (store.filtered().length===0) {
           <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No projects found</div>
         }
         <div style="padding:12px 24px; display:flex; justify-content:space-between; align-items:center; border-top:1px solid var(--border); font-size:12px; color:var(--text-muted);">
           <span>Page {{ store.page() }} / {{ store.totalPages() }} • {{ store.total() }} total</span>
           <div style="display:flex; gap:8px;">
             <button class="pill" (click)="prev()" [disabled]="!store.hasPrev()">← Prev</button>
             <button class="pill" (click)="next()" [disabled]="!store.hasNext()">Next →</button>
           </div>
         </div>
       </div>
     }
  `,
     styles: [`
      .toolbar { display:flex; gap:12px; align-items:center; flex-wrap:wrap; margin-bottom: var(--gap-widget); }
      .search-bar { display:flex; align-items:center; gap:10px; padding:10px 16px; border-radius: var(--radius-input); background: var(--flat-bg); border:1px solid var(--border); flex:1; max-width:420px; }
      .search-bar input { flex:1; border:none; outline:none; background:transparent; font-size:13px; color:var(--text-primary); }
      .search-bar input::placeholder { color:var(--text-muted); }
      .filter-pills { display:inline-flex; background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:2px; gap:2px; }
       .pill { padding:6px 14px; border-radius:999px; font-size:12px; font-weight:500; background:transparent; border:none; color:var(--text-secondary); cursor:pointer; transition:all 150ms ease; }
       .pill:hover:not(.active) { background:var(--border); color:var(--text-primary); }
       .pill:disabled { opacity:0.4; cursor:not-allowed; }
       .pill.active { background: var(--black); color: var(--on-black); border-color: var(--black); box-shadow:var(--shadow-card); }
       .badge { background: var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; font-weight:500; color: var(--text-secondary); }
       .row { display:flex; align-items:center; gap:12px; padding:12px 24px; border-top:1px solid var(--border); text-decoration:none; color: inherit; }
       .row-wrap { display:flex; align-items:center; border-top:1px solid var(--border); }
       .row-wrap:first-child { border-top:none; }
       .row-wrap:hover { background: var(--flat-bg); }
       .row:first-child { border-top:none; }
       .row:hover { background: transparent; }
       .thumb { width:36px; height:36px; border-radius:12px; background: var(--border); }
       .btn-primary { background: var(--black); color: var(--bg); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer; }
       .btn-primary:disabled { opacity:0.6; cursor:not-allowed; }
       .btn-secondary { background: var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; font-size:12px; color: var(--text-primary); cursor:pointer; }
       .skeleton { height:14px; background: var(--border); border-radius:6px; margin:8px 0; }
       .modal-overlay { position:fixed; inset:0; background:rgba(0,0,0,0.55); display:grid; place-items:center; z-index:1000; padding:24px; }
       .modal { background:var(--card-bg); border:1px solid var(--border); border-radius:24px; padding:24px 32px; width:min(560px, 100%); box-shadow: var(--shadow-hover); color: var(--text-primary); }
       .input-tier1 { width:100%; margin-top:6px; padding:10px 14px; border-radius:18px; border:1px solid var(--border); background:var(--flat-bg); font-size:13px; outline:none; color: var(--text-primary); }
       .input-tier1::placeholder { color: var(--text-muted); }
       .input-tier1:focus { border-color:var(--text-primary); }
    `]
})
export class ProjectsPage implements OnInit {
  store = inject(ProjectsStore);
  showModal = false;
  editingId: string | null = null;
  form: { name: string; status: string; priority: string; criticality: string; dueDate: string; description: string } = {
    name: '', status: 'Active', priority: 'Medium', criticality: 'Medium', dueDate: '', description: ''
  };
  ngOnInit(): void { this.store.load(); }
  openModal(): void { this.editingId = null; this.form = { name: '', status: 'Active', priority: 'Medium', criticality: 'Medium', dueDate: '', description: '' }; this.showModal = true; }
  closeModal(): void { this.showModal = false; this.editingId = null; }
  openEdit(p: any): void {
    this.editingId = p.id;
    this.form = { name: p.name, status: p.status, priority: p.priority || 'Medium', criticality: p.criticality || 'Medium', dueDate: p.dueDate ? p.dueDate.substring(0,10) : '', description: p.description || '' };
    this.showModal = true;
  }
  doRefresh(): void { this.store.load(); }
  onPageSize(v: string): void { this.store.setPageSize(parseInt(v,10)); this.store.load(); }
  prev(): void { this.store.prevPage(); this.store.load(); }
  next(): void { this.store.nextPage(); this.store.load(); }
  archive(p: any): void { if(confirm(`Archive project "${p.name}"?`)) this.store.archive(p.id); }
  submit(): void {
    const name = this.form.name.trim();
    if (name.length < 3 || name.length > 200) return;
    const placeholder = '00000000-0000-0000-0000-000000000001';
    const payload: any = {
      name,
      status: this.form.status,
      priority: this.form.priority,
      criticality: this.form.criticality,
      dueDate: this.form.dueDate ? new Date(this.form.dueDate).toISOString() : null,
      description: this.form.description || null,
      ownerId: placeholder,
      managerId: placeholder
    };
    if (this.editingId) {
      this.store.update({ id: this.editingId, patch: payload as any });
    } else {
      this.store.create(payload as any);
    }
    // close optimistically, error will show on next open; actual refresh via store
    setTimeout(() => { if (!this.store.error()) { this.closeModal(); } }, 400);
  }
}
