import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { map } from 'rxjs';

export const RoleGuard: CanActivateFn = (route) => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  const requiredRoles: string[] | undefined = route.data?.['roles'];
  const requiredPermission: string | undefined = route.data?.['permission'];

  return oidc.isAuthenticated$.pipe(
    map(({ isAuthenticated }) => {
      if (!isAuthenticated) {
        router.navigate(['/']);
        return false;
      }
      // getPayloadFromIdToken is Observable in angular-auth-oidc-client v17 — need to read sync from storage
      let payload: any = null;
      try {
        const raw = sessionStorage.getItem('0-orokanban-web');
        if (raw) {
          const parsed = JSON.parse(raw);
          payload = parsed?.userData ?? parsed?.authnResult?.userData ?? null;
          // fallback: try to decode id_token if userData missing
          if (!payload && parsed?.authnResult?.id_token) {
            const parts = parsed.authnResult.id_token.split('.');
            if (parts.length >= 2) payload = JSON.parse(atob(parts[1].replace(/-/g,'+').replace(/_/g,'/')));
          }
        }
      } catch {}
      const rawRoles: any[] = payload?.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : (payload?.roles ? (Array.isArray(payload.roles)? payload.roles : [payload.roles]) : []);
      const roles: string[] = rawRoles.map((r:any)=> typeof r === 'string' ? r : r?.value ?? r?.Value ?? r?.name ?? r?.Name ?? '').filter(Boolean);
      if (requiredRoles && !requiredRoles.some(r => roles.includes(r))) {
        router.navigate(['/dashboard']);
        return false;
      }
      if (requiredPermission && !roles.includes(requiredPermission) && !roles.includes('Administrator') && !roles.includes('RootManager')) {
        if (requiredPermission === 'audit.read' && !roles.includes('Auditor')) {
          router.navigate(['/dashboard']);
          return false;
        }
        if (requiredPermission !== 'audit.read') {
          router.navigate(['/dashboard']);
          return false;
        }
      }
      return true;
    })
  );
};

export const AuthGuard: CanActivateFn = (route, state) => {
  const oidc = inject(OidcSecurityService);
  const router = inject(Router);
  return oidc.isAuthenticated$.pipe(
    map(({ isAuthenticated }) => {
      if (!isAuthenticated) {
        oidc.authorize();
        return false;
      }
      return true;
    })
  );
};
