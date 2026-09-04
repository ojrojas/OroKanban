import { Directive, Input, TemplateRef, ViewContainerRef, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Directive({ selector: '[hasPermission]', standalone: true })
export class HasPermissionDirective {
  private template = inject(TemplateRef<any>);
  private view = inject(ViewContainerRef);
  private oidc = inject(OidcSecurityService);
  private hasView = false;

  @Input('hasPermission') permission: string | null = null;
  @Input('hasPermissionRoles') rolesInput: string[] | null = null;

  ngOnInit() {
    let payload: any = null;
    try {
      const raw = sessionStorage.getItem('0-orokanban-web');
      if (raw) {
        const parsed = JSON.parse(raw);
        payload = parsed?.userData ?? parsed?.authnResult?.userData ?? null;
        if (!payload && parsed?.authnResult?.id_token) {
          const parts = parsed.authnResult.id_token.split('.');
          if (parts.length >= 2) payload = JSON.parse(atob(parts[1].replace(/-/g,'+').replace(/_/g,'/')));
        }
      }
      // also try direct userData storage key
      if (!payload) {
        try { const alt = sessionStorage.getItem('userData'); if (alt) payload = JSON.parse(alt); } catch {}
      }
    } catch {}
    const rawRoles: any[] = payload?.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : (payload?.roles ? (Array.isArray(payload.roles)? payload.roles : [payload.roles]) : []);
    const roles: string[] = rawRoles.map((r:any)=> typeof r === 'string' ? r : r?.value ?? r?.Value ?? r?.name ?? r?.Name ?? '').filter(Boolean);
    const required = this.permission || (this.rolesInput ? this.rolesInput.join(',') : null);
    let visible = true;
    if (this.permission) {
      visible = roles.includes(this.permission) || roles.includes('Administrator') || roles.includes('RootManager');
      if (this.permission === 'audit.read' && !roles.includes('Auditor') && !roles.includes('Administrator') && !roles.includes('RootManager')) visible = false;
    }
    if (this.rolesInput) {
      visible = this.rolesInput.some(r => roles.includes(r));
    }
    if (visible && !this.hasView) {
      this.view.createEmbeddedView(this.template);
      this.hasView = true;
    } else if (!visible && this.hasView) {
      this.view.clear();
      this.hasView = false;
    }
  }
}
