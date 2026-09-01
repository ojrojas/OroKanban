import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { switchMap } from 'rxjs';
import { environment } from '../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const oidc = inject(OidcSecurityService);
  const apiUrl = environment.apiUrl;
  const isApi = req.url.startsWith(apiUrl) || req.url.includes('/api/') || (apiUrl.startsWith('http') ? req.url.startsWith(apiUrl) : false) || req.url.startsWith(window.location.origin + apiUrl);
  if (!isApi) {
    return next(req);
  }
  return oidc.getAccessToken().pipe(
    switchMap((token) => {
      if (token) {
        req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
      }
      return next(req);
    })
  );
};
