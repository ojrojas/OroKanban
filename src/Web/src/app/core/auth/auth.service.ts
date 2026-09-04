import { Injectable, inject } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private oidc = inject(OidcSecurityService);

  isAuthenticated$ = this.oidc.isAuthenticated$;
  userData$ = this.oidc.userData$;

  login() { this.oidc.authorize(); }

  /**
   * Logout integral: limpia App (storage + SignalR) + Api (revocación tokens)
   * + IdentityServer (end_session). Garantiza que el botón siempre navegue,
   * incluso si el IdP no tiene post_logout_redirect_uri registrado.
   */
  logout(): void {
    // 1) Intentar obtener id_token ANTES de limpiar storage (necesario para id_token_hint)
    let idToken: string | null = null;
    try {
      const stored = sessionStorage.getItem('id_token') || localStorage.getItem('id_token');
      if (stored) idToken = stored;
    } catch {}
    // Fallback: intentar leer del storage del OIDC client (clave con prefijo)
    if (!idToken) {
      try {
        for (let i = 0; i < sessionStorage.length; i++) {
          const k = sessionStorage.key(i) || '';
          if (k.includes('orokanban') || k.includes('authnResult') || k.includes('id_token')) {
            const v = sessionStorage.getItem(k);
            if (v && v.includes('id_token')) {
              try { const p = JSON.parse(v); if (p?.id_token) idToken = p.id_token; } catch {}
            }
          }
        }
      } catch {}
    }
    // Ahora limpiar solo lo no esencial (no la sesión OIDC aún, la necesita logoffAndRevokeTokens)
    try { localStorage.removeItem('game-cache'); } catch {}
    try { sessionStorage.removeItem('game-cache'); } catch {}
    try { sessionStorage.removeItem('idemp-withdraw'); } catch {}
    // Intentar desconectar SignalR global si está activo (evita reconexión tras logout)
    try {
      const anyWindow = window as any;
      if (anyWindow.__gameRealtimeDisconnect) anyWindow.__gameRealtimeDisconnect();
    } catch {}

    // 2) Api es stateless (JWT) pero revocamos refresh/access en IdP
    //    y limpiamos cualquier token en memoria del OIDC client
    const doLocalCleanup = () => {
      try { (this.oidc as any).logoffLocal?.(); } catch {}
      try { sessionStorage.clear(); localStorage.clear(); } catch {}
    };

    const doFallbackRedirect = (reason: string) => {
      doLocalCleanup();
      console.warn(`[Auth] logout fallback (${reason}) -> /auth/logout-callback`);
      const fallbackUrl = window.location.origin + '/auth/logout-callback';
      if (!window.location.pathname.includes('/auth/logout-callback')) {
        // Pequeño delay para dar tiempo a que logoffLocal limpie
        setTimeout(() => (window.location.href = fallbackUrl), 200);
      }
    };

    // idToken ya obtenido arriba antes de limpiar storage
    // 3) IdentityServer: end_session + revocación
    // Primero intentar el flujo de la librería (revoca y hace redirect a end_session)
    try {
      const obs: any = (this.oidc as any).logoffAndRevokeTokens?.();
      if (obs && typeof obs.subscribe === 'function') {
        console.log('[Auth] logoffAndRevokeTokens -> IdP');
        obs.subscribe({
          next: () => {
            console.log('[Auth] logoffAndRevokeTokens success');
            doLocalCleanup();
          },
          error: (e: any) => {
            console.warn('[Auth] logoffAndRevokeTokens error', e);
            // Fallback manual a end_session si la librería falla
            this.manualIdpLogout(idToken);
          },
        });
        // Si en 1.8s no hubo navegación, forzar manual (para OroKanban: si sigue en app y no en logout)
        setTimeout(() => {
          if (!window.location.href.includes('connect/logout') && !window.location.pathname.includes('/auth/logout-callback') && !window.location.href.includes('/Account/Logout')) {
            console.warn('[Auth] logoffAndRevokeTokens no navegó -> manual');
            this.manualIdpLogout(idToken);
          }
        }, 1800);
        return;
      }
    } catch (e) {
      console.warn('[Auth] logoffAndRevokeTokens threw', e);
    }

    try {
      console.log('[Auth] logoff() -> IdP end_session');
      this.oidc.logoff();
      setTimeout(() => {
        if (!window.location.href.includes('connect/logout') && !window.location.pathname.includes('/auth/logout-callback') && !window.location.href.includes('/Account/Logout')) {
          this.manualIdpLogout(idToken);
        }
      }, 1500);
      return;
    } catch (e) {
      console.warn('[Auth] logoff() threw', e);
    }
    doFallbackRedirect('exception');
  }

  /** Fallback manual: navega directo a IdP /connect/logout con id_token_hint */
  private async manualIdpLogout(idToken: string | null) {
    // Resolver id_token async si no se pasó
    if (!idToken) {
      try {
        idToken = await firstValueFrom((this.oidc as any).getIdToken?.() ?? (async () => null)());
      } catch { idToken = null; }
      // Fallback storage si Observable no emite
      if (!idToken) {
        try { idToken = sessionStorage.getItem('id_token') || localStorage.getItem('id_token'); } catch {}
      }
    }
    try {
      const postLogout = encodeURIComponent(window.location.origin + '/auth/logout-callback');
      const authority = (environment as any).identityAuthority?.replace(/\/$/, '') ?? 'https://localhost:5086';
      // Intentar revocar refresh token manualmente antes de salir
      try { (this.oidc as any).revokeRefreshToken?.().subscribe?.(() => {}); } catch {}
      try { (this.oidc as any).revokeAccessToken?.().subscribe?.(() => {}); } catch {}
      // Limpiar local antes de salir (solo keys OIDC, no todo localStorage agresivo)
      try { (this.oidc as any).logoffLocal?.(); } catch {}
      try { sessionStorage.clear(); } catch {}
      let url = `${authority}/connect/logout?post_logout_redirect_uri=${postLogout}`;
      if (idToken) url += `&id_token_hint=${encodeURIComponent(idToken)}`;
      console.log('[Auth] manualIdpLogout ->', url);
      window.location.href = url;
      // Si el IdP no responde (cert/CORS), el navegador mostrará error; fallback a local en 2s
      setTimeout(() => {
        if (!window.location.pathname.includes('/auth/logout-callback')) {
          window.location.href = window.location.origin + '/auth/logout-callback';
        }
      }, 2000);
    } catch {
      window.location.href = window.location.origin + '/auth/logout-callback';
    }
  }

  logoffLocal(): void {
    try { (this.oidc as any).logoffLocal?.(); } catch {}
    try { sessionStorage.clear(); localStorage.clear(); } catch {}
    window.location.href = window.location.origin + '/auth/logout-callback';
  }

  getAccessToken(): import('rxjs').Observable<string> { return this.oidc.getAccessToken(); }
  getPayload(): import('rxjs').Observable<any> { return this.oidc.getPayloadFromIdToken(); }
}
