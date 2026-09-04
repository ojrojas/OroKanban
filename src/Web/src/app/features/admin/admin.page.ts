import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AdminStore } from './admin.store';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  providers: [AdminStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Administration</h1>
      <p class="page-header__subtitle">Organization hierarchy and roles — crea unidades organizacionales para el árbol de permisos</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
       <div class="tier-2" style="padding:24px 32px;">
         <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:12px;">
           <h3 style="margin:0; font-size:14px; font-weight:700;">Organization units ({{filteredUnits().length}}/{{store.units().length}})</h3>
           <div style="display:flex; gap:8px; align-items:center;">
             <div class="search-bar tier-1" style="padding:6px 12px; border-radius:18px; background:var(--flat-bg); border:1px solid var(--border); display:flex; gap:8px; align-items:center;">
               <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
               <input placeholder="Filtrar unidades..." [value]="q()" (input)="q.set($any($event.target).value)" style="border:none; outline:none; background:transparent; font-size:13px; color:var(--text-primary); min-width:160px;" />
             </div>
             <button class="pill" (click)="store.load()" title="Recargar">Refresh</button>
           </div>
         </div>
         <p style="font-size:12px; color:var(--text-muted); margin-bottom:12px;">Unidades desde <code>/api/admin/organization-units</code> (o sintéticas de IdentityServer). Se usan para el <i>subtree</i> de permisos de cada manager.</p>
         @for (u of filteredUnits(); track u.id || u.name) {
           <div class="row"><div class="thumb"></div><div style="flex:1"><div style="font-weight:600; font-size:13px;">{{u.name ?? u.title}} <span style="font-size:11px; color:var(--text-muted);">{{u.hierarchyPath ?? u.HierarchyPath}}</span></div><div style="font-size:12px; color:var(--text-secondary);">{{u.type ?? 'Unit'}} • {{u.id}}</div></div><span class="badge">{{u.role ?? 'Member'}}</span></div>
         }
          @if (filteredUnits().length===0) { <div style="padding:24px; text-align:center; color:var(--text-muted); font-size:13px;">No units — prueba con otro filtro o "Refresh"</div> }
       </div>
       <div class="tier-2" style="padding:24px 32px; margin-top:var(--gap-widget);">
         <h3 style="margin:0 0 6px; font-size:14px; font-weight:700;">Create unit</h3>
         <p style="font-size:12px; color:var(--text-muted); margin-bottom:10px;"><b>¿Para qué sirve?</b> Crea una unidad organizacional (departamento/equipo) dentro de la jerarquía. El <code>Parent</code> define bajo qué nodo cuelga; si es vacío crea una raíz. El <code>HierarchyPath</code> se genera automáticamente y se usa para evaluar el <i>subtree</i> de cada manager (quién puede ver/editar proyectos/tareas).</p>
         @if(createError()){ <div style="color:var(--red-text); font-size:12px; margin-bottom:8px;">{{createError()}}</div> }
         @if(createOk()){ <div style="color:var(--green-text); font-size:12px; margin-bottom:8px;">Unidad creada ✔</div> }
         <div style="display:flex; gap:12px; flex-wrap:wrap; align-items:end;">
           <label style="flex:2; font-size:12px; font-weight:600;">Name * (2..200)
             <input [(ngModel)]="newName" placeholder="Ej: Team Alpha" style="width:100%; margin-top:6px; padding:10px 16px; border-radius:18px; border:1px solid var(--border);" />
           </label>
           <label style="flex:1; font-size:12px; font-weight:600;">Parent (opcional)
             <select [(ngModel)]="newParentId" style="width:100%; margin-top:6px; padding:10px 16px; border-radius:18px; border:1px solid var(--border); background:var(--flat-bg);">
               <option [ngValue]="null">— Raíz (sin padre) —</option>
               @for (u of store.units(); track u.id){ <option [ngValue]="u.id">{{u.name}} ({{u.id.slice(0,8)}}…)</option> }
             </select>
           </label>
           <button class="btn-primary" (click)="createUnit()" [disabled]="!newName.trim()">Create</button>
         </div>
       </div>
     }
   `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .row:first-child{border-top:none;} .thumb{width:36px; height:36px; border-radius:12px; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-primary{background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; cursor:pointer;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class AdminPage implements OnInit {
  store = inject(AdminStore);
  private http = inject(HttpClient);
  newName = '';
  newParentId: string | null = null;
  createError = signal<string|null>(null);
  createOk = signal(false);
  q = signal('');
  filteredUnits = computed(() => {
    const query = this.q().toLowerCase().trim();
    const units = this.store.units();
    if (!query) return units;
    return units.filter((u:any) => (u.name ?? u.title ?? '').toLowerCase().includes(query) || (u.hierarchyPath ?? u.HierarchyPath ?? '').toLowerCase().includes(query) || (u.id ?? '').toLowerCase().includes(query));
  });
  ngOnInit(): void { this.store.load(); }
  createUnit(){
    const name = this.newName.trim();
    if(name.length<2){ this.createError.set('Name 2..200'); return; }
    this.createError.set(null); this.createOk.set(false);
    const body:any = { name, parentId: this.newParentId || null };
    this.http.post('/api/admin/organization-units', body).subscribe({
      next: ()=> { this.createOk.set(true); this.newName=''; this.newParentId=null; this.store.load(); setTimeout(()=>this.createOk.set(false),2000); },
      error: (e:any)=> this.createError.set(e?.error?.detail ?? e?.error?.title ?? 'create failed')
    });
  }
}
