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
    const payload = this.oidc.getPayloadFromIdToken() as any;
    const roles: string[] = payload?.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [];
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
