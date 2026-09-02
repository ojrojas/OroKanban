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
      const payload = oidc.getPayloadFromIdToken() as any;
      const roles: string[] = payload?.role ? (Array.isArray(payload.role) ? payload.role : [payload.role]) : [];
      if (requiredRoles && !requiredRoles.some(r => roles.includes(r))) {
        router.navigate(['/dashboard']);
        return false;
      }
      if (requiredPermission && !roles.includes(requiredPermission) && !roles.includes('Administrator') && !roles.includes('RootManager')) {
        // Check via permission catalog would require API call; for now deny if role doesn't match permission hint
        // API remains sole authority — guard only hides, not secures
        // Allow Auditor for audit.read explicitly
        if (requiredPermission === 'audit.read' && !roles.includes('Auditor')) {
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
