import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Router } from '@angular/router';

@Component({
  selector: 'app-logout-callback',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="logout-page" [attr.data-theme]="theme">
      <div class="tier-2 logout-card">
        @if (checking) {
          <div class="spinner" role="status" aria-label="Verificando sesión"></div>
          <h1 class="logout-title">Verificando sesión...</h1>
          <p class="logout-subtitle">Comprobando el estado en IdentityServer, OroKanban Api y la App local.</p>
          <p class="logout-url">{{ currentUrl }}</p>
        } @else if (isCancelled) {
          <div class="icon-cancel">↩</div>
          <h1 class="logout-title">Has cancelado el cierre de sesión</h1>
          <p class="logout-subtitle">Sigues autenticado. No se ha limpiado la sesión de IdentityServer ni de OroKanban.</p>
          <p class="logout-actions"><a routerLink="/dashboard" class="btn-primary">Volver al dashboard</a> · <a (click)="logout()" style="cursor:pointer; text-decoration:underline; color:var(--text-secondary);">Intentar cerrar de nuevo</a></p>
          @if (error) { <p role="alert" class="logout-error">{{ error }}</p> }
        } @else {
          <div class="icon-success">✓</div>
          <h1 class="logout-title">Sesión cerrada correctamente</h1>
          <p class="logout-subtitle">IdentityServer + Api + App limpiadas. Has salido de OroKanban y volverás a la app (no al home del IdentityServer) gracias a <code>post_logout_redirect_uri</code>.</p>
          <p class="logout-actions"><a routerLink="/" class="btn-primary">Volver al inicio</a> · <a (click)="login()" style="cursor:pointer; text-decoration:underline; color:var(--text-secondary);">Iniciar sesión de nuevo</a></p>
          @if (error) { <p role="alert" class="logout-error">{{ error }}</p> }
          <p class="logout-url">{{ currentUrl }}</p>
        }
      </div>
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:var(--bg); color:var(--text-primary); }
    .logout-page { min-height:100vh; display:grid; place-items:center; padding:24px; background:var(--bg); }
    .logout-card { padding:32px 28px; text-align:center; max-width:480px; width:100%; display:flex; flex-direction:column; align-items:center; gap:12px; }
    .spinner { width:36px; height:36px; border:3px solid var(--border); border-top-color:var(--text-primary); border-radius:50%; animation:spin 0.8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .icon-success { width:48px; height:48px; border-radius:50%; background:var(--green-bg, #E8F9EC); color:var(--green-text, #2FA84A); display:grid; place-items:center; font-size:22px; font-weight:700; }
    .icon-cancel { width:48px; height:48px; border-radius:50%; background:var(--flat-bg); border:1px solid var(--border); color:var(--text-secondary); display:grid; place-items:center; font-size:20px; }
    .logout-title { margin:0; font-size:18px; font-weight:700; color:var(--text-primary); }
    .logout-subtitle { margin:0; font-size:13px; color:var(--text-secondary); }
    .logout-actions { margin:8px 0 0; font-size:13px; }
    .logout-error { color:var(--red-text, #DC2626); font-size:12px; background:var(--red-bg, #FFF2F2); border:1px solid var(--border); padding:8px 12px; border-radius:12px; }
    .logout-url { margin:8px 0 0; font-size:10px; color:var(--text-muted); word-break:break-all; max-width:100%; opacity:0.7; }
    .btn-primary { background:var(--black); color:var(--on-black); border:none; border-radius:999px; padding:8px 16px; font-size:12px; font-weight:600; cursor:pointer; text-decoration:none; display:inline-block; }
  `]
})
export class LogoutCallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  error = '';
  theme: string = 'light';
  currentUrl: string = '';
  checking = true;
  isCancelled = false;

  ngOnInit(): void {
    try {
      const saved = localStorage.getItem('orokanban-theme') as string | null;
      this.theme = saved === 'dark' || saved === 'light' ? saved : (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      document.documentElement.dataset['theme'] = this.theme;
    } catch {}
    try { this.currentUrl = window.location.href; } catch {}
    // No limpiar storage antes de checkAuth — necesitamos saber si el usuario canceló (sigue autenticado) o confirmó (ya deslogueado)
    // Limpiar solo claves de juego, no la sesión OIDC aún
    try { sessionStorage.removeItem('idemp-withdraw'); } catch {}
    try { localStorage.removeItem('game-cache'); } catch {}
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated }) => {
        this.checking = false;
        if (isAuthenticated) {
          // Usuario canceló en la pantalla del IdentityServer o el IdP no deslogueó — sigue autenticado, NO limpiar sesión
          console.log('[logout-callback] aún autenticado — cancelado o sesión activa, permaneciendo logueado');
          this.error = 'Has cancelado el cierre de sesión. Sigues autenticado.';
          this.isCancelled = true;
          // No hacemos logoffLocal ni clear — mantenemos sesión para volver a la app (OroKanban, no home del IdP)
          setTimeout(() => this.router.navigateByUrl('/dashboard'), 1800);
        } else {
          // Confirmado: ya deslogueado en IdP — limpiar App por completo y mostrar mensaje de sesión cerrada
          console.log('[logout-callback] deslogueado confirmado — limpiando App');
          try { (this.oidc as any).logoffLocal?.(); } catch {}
          try { sessionStorage.clear(); localStorage.clear(); } catch {}
          this.isCancelled = false;
          // Navegar al inicio donde se ve "Iniciar sesión" (estado deslogueado en los 3 layers) — gracias a post_logout_redirect_uri volverá a OroKanban
          setTimeout(() => this.router.navigateByUrl('/'), 900);
        }
      },
      error: (e) => {
        console.warn('[logout-callback] checkAuth error', e);
        this.checking = false;
        // IdP caído — fallback a limpieza local y volver a app (no a home del IdP)
        try { (this.oidc as any).logoffLocal?.(); } catch {}
        try { sessionStorage.clear(); localStorage.clear(); } catch {}
        this.error = 'Sesión cerrada localmente (IdP no disponible). Volverás a OroKanban.';
        this.router.navigateByUrl('/');
      },
    });
    // Fallback si checkAuth no emite (IdP caído)
    setTimeout(() => {
      if (this.checking && window.location.pathname.includes('/auth/logout-callback')) {
        this.checking = false;
        this.router.navigateByUrl('/');
      }
    }, 3500);
  }

  login() { (this.oidc as any).authorize?.(); }
  logout() {
    try { (this.oidc as any).logoffLocal?.(); } catch {}
    try { sessionStorage.clear(); localStorage.clear(); } catch {}
    window.location.href = window.location.origin + '/auth/logout-callback';
  }
}
