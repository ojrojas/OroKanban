import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';

export const tenantInterceptor: HttpInterceptorFn = (req, next) => {
  const oidc = inject(OidcSecurityService);
  const payload = oidc.getPayloadFromIdToken() as any;
  const tenantId = payload?.tenant_id;
  if (tenantId) {
    return next(req.clone({ setHeaders: { 'X-Tenant-Id': tenantId } }));
  }
  return next(req);
};
