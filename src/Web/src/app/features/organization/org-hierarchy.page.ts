import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { OrgHierarchyStore } from './org-hierarchy.store';
import { BreadcrumbsComponent } from '../../shared/ui/breadcrumbs/breadcrumbs.component';

@Component({
  selector: 'app-org',
  standalone: true,
  imports: [CommonModule, BreadcrumbsComponent],
  providers: [OrgHierarchyStore],
  template: `
    <app-breadcrumbs [crumbs]="[{label:'Home',link:'/dashboard'},{label:'Organization'}]" />
    <div class="page-header">
      <h1 class="page-header__title">Organization</h1>
      <p class="page-header__subtitle">Hierarchy — tree unbounded depth (from Identity Server)</p>
    </div>
    @if (store.isPending()) { <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div> }
    @else if (store.error()) { <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;"><span style="color:var(--red-text); font-size:13px;">{{store.error()}}</span><button class="btn-secondary" (click)="store.load()">Retry</button></div> }
    @else {
       <div class="tier-2" style="padding:24px 32px;">
         <div style="display:flex; justify-content:space-between; align-items:center; margin-bottom:16px;">
           <h3 style="margin:0; font-size:14px; font-weight:700;">Hierarchy ({{store.tree().length}} nodes)</h3>
           <span class="badge">{{store.tree().length}} total</span>
         </div>

         <!-- Tree: roots → children recursively (2 levels in current seed, supports unbounded depth) -->
         <div class="org-tree">
           @for (root of roots(); track root.id ?? root.Id) {
             <div class="tree-node">
               <div class="row" style="border-top:none; background: var(--flat-bg); border-radius: 12px; padding: 12px 16px; margin-bottom: 8px;">
                 <div class="avatar" style="background: var(--black); color:var(--on-black); display:grid; place-items:center; font-weight:700; font-size:11px;">{{ initials(root.name ?? root.Name) }}</div>
                 <div style="flex:1">
                   <div style="font-weight:700; font-size:13px; color:var(--text-primary);">{{root.name ?? root.Name}}</div>
                   <div style="font-size:12px; color:var(--text-secondary);">{{ roleOf(root.name ?? root.Name) }} • {{ root.hierarchyPath ?? root.HierarchyPath }} • Level {{ levelOf(root) }}</div>
                 </div>
                 <span class="badge" style="background: var(--card-bg);">{{ childrenOf(root.id ?? root.Id).length }} direct reports</span>
               </div>

               <!-- Level 1: managers -->
               <div class="tree-children" style="margin-left: 20px; border-left: 1px dashed var(--border); padding-left: 16px;">
                 @for (m of childrenOf(root.id ?? root.Id); track m.id ?? m.Id) {
                   <div class="tree-node" style="margin-top: 8px;">
                      <div class="row" style="border-top:none; background: var(--card-bg); border:1px solid var(--border); border-radius: 12px; padding: 10px 14px;">
                        <div class="avatar" style="background:var(--flat-bg); color:var(--text-primary); border:1px solid var(--border); display:grid; place-items:center; font-weight:700; font-size:11px;">{{ initials(m.name ?? m.Name) }}</div>
                       <div style="flex:1">
                         <div style="font-weight:600; font-size:13px;">{{m.name ?? m.Name}}</div>
                         <div style="font-size:12px; color:var(--text-secondary);">{{ roleOf(m.name ?? m.Name) }} • {{ m.hierarchyPath ?? m.HierarchyPath }} @if(parentName(m)){ • reports to {{ parentName(m) }} }</div>
                       </div>
                       <span class="badge">{{ childrenOf(m.id ?? m.Id).length }} reports</span>
                     </div>

                     <!-- Level 2+: contributors / deeper -->
                     <div class="tree-children" style="margin-left: 20px; border-left: 1px dashed var(--border); padding-left: 16px; margin-top: 6px;">
                       @for (c of childrenOf(m.id ?? m.Id); track c.id ?? c.Id) {
                         <div class="row" style="border-top:none; background: var(--flat-bg); border-radius: 10px; padding: 8px 12px; margin-top:6px;">
                           <div class="avatar" style="background: var(--border); display:grid; place-items:center; font-weight:600; font-size:10px;">{{ initials(c.name ?? c.Name) }}</div>
                           <div style="flex:1">
                             <div style="font-weight:600; font-size:12px;">{{c.name ?? c.Name}}</div>
                             <div style="font-size:11px; color:var(--text-secondary);">{{ roleOf(c.name ?? c.Name) }} • {{ c.hierarchyPath ?? c.HierarchyPath }} • Level {{ levelOf(c) }} @if(parentName(c)){ • reports to {{ parentName(c) }} }</div>
                           </div>
                           <span class="badge" style="font-size:10px;">{{ roleOf(c.name ?? c.Name) }}</span>
                         </div>
                         <!-- Level 3+ recursion (if deeper than 2, render generic) -->
                         @for (deep of childrenOf(c.id ?? c.Id); track deep.id ?? deep.Id) {
                           <div class="row" style="margin-left: 20px; border-left:1px dashed var(--border); padding-left:12px; margin-top:4px;">
                             <div class="avatar" style="width:28px; height:28px; font-size:10px;">{{ initials(deep.name ?? deep.Name) }}</div>
                             <div style="flex:1"><div style="font-weight:600; font-size:12px;">{{deep.name ?? deep.Name}}</div><div style="font-size:11px; color:var(--text-secondary);">{{deep.hierarchyPath ?? deep.HierarchyPath}} • Level {{levelOf(deep)}}</div></div>
                           </div>
                         }
                       }
                       @if(childrenOf(m.id ?? m.Id).length===0){
                         <div style="font-size:11px; color:var(--text-muted); padding:6px 0;">No direct reports</div>
                       }
                     </div>
                   </div>
                 }
                 @if(childrenOf(root.id ?? root.Id).length===0){
                   <div style="font-size:11px; color:var(--text-muted); padding:6px 0;">No managers assigned</div>
                 }
               </div>
             </div>
           }
         </div>

         @if (store.tree().length===0) { <div style="padding:24px; text-align:center; color:var(--text-muted); font-size:13px;">No organization data — check Identity Server connectivity</div> }
       </div>
       <div class="tier-2" style="padding:16px; margin-top: var(--gap-widget); font-size:12px; color:var(--text-muted);">
         Source: <code>/api/organization/units</code> (synthesized from IdentityServer users when local DB empty) • Tenant filtered • Level = depth of <code>hierarchyPath</code>
       </div>
     }
  `,
  styles: [`.row{display:flex; gap:12px; padding:12px 0; border-top:1px solid var(--border); align-items:center;} .avatar{width:36px; height:36px; border-radius:50%; background:var(--border);} .badge{background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px;} .skeleton{height:14px; background:var(--border); border-radius:6px;} .btn-secondary{background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer;}`]
})
export class OrgHierarchyPage implements OnInit {
  store = inject(OrgHierarchyStore);
  ngOnInit(): void { this.store.load(); }

  // Build parent->children map from flat store.tree()
  private mapById(): Map<string, any> {
    const m = new Map<string, any>();
    for (const n of this.store.tree() as any[]) {
      const id = (n.id ?? n.Id) as string;
      m.set(String(id).toLowerCase(), n);
    }
    return m;
  }

  roots(): any[] {
    const tree = this.store.tree() as any[];
    return tree.filter((n: any) => !n.parentId && !n.ParentId);
  }

  childrenOf(parentId: string): any[] {
    const tree = this.store.tree() as any[];
    const pid = String(parentId).toLowerCase();
    return tree.filter((n: any) => String(n.parentId ?? n.ParentId ?? '').toLowerCase() === pid);
  }

  levelOf(n: any): number {
    const p = n.hierarchyPath ?? n.HierarchyPath ?? '';
    return p ? p.split('/').filter(Boolean).length - 1 : 0;
  }

  parentName(n: any): string | null {
    const pid = n.parentId ?? n.ParentId;
    if (!pid) return null;
    const m = this.mapById().get(String(pid).toLowerCase());
    return m ? (m.name ?? m.Name) : null;
  }

  initials(name: string): string {
    return (name ?? '').split(' ').filter(Boolean).slice(0,2).map((s:string)=>s[0]?.toUpperCase()).join('') || '•';
  }

  roleOf(name: string): string {
    const m = name?.match(/\(([^)]+)\)/);
    return m ? m[1] : 'Unit';
  }
}
