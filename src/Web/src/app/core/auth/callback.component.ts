import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { take } from 'rxjs';

@Component({
  selector: 'app-callback',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="callback-page" [attr.data-theme]="theme">
      <div class="tier-2 callback-card">
        <div class="spinner" role="status" aria-label="Autenticando"></div>
        <h1 class="callback-title">Autenticando...</h1>
        <p class="callback-subtitle">Conectando con IdentityServer y validando tu sesión en OroKanban.</p>
        <p class="callback-hint">Si quedas aquí más de 3s, abre F12 → Console → [callback] para ver detalles.</p>
        <p class="callback-url">{{ currentUrl }}</p>
      </div>
    </div>
  `,
  styles: [`
    :host { display:block; min-height:100vh; background:var(--bg); color:var(--text-primary); }
    .callback-page { min-height:100vh; display:grid; place-items:center; padding:24px; background:var(--bg); }
    .callback-card { padding:32px 28px; text-align:center; max-width:420px; width:100%; display:flex; flex-direction:column; align-items:center; gap:12px; }
    .spinner { width:36px; height:36px; border:3px solid var(--border); border-top-color:var(--text-primary); border-radius:50%; animation:spin 0.8s linear infinite; }
    @keyframes spin { to { transform: rotate(360deg); } }
    .callback-title { margin:0; font-size:18px; font-weight:700; color:var(--text-primary); }
    .callback-subtitle { margin:0; font-size:13px; color:var(--text-secondary); }
    .callback-hint { margin:0; font-size:11px; color:var(--text-muted); }
    .callback-url { margin:8px 0 0; font-size:10px; color:var(--text-muted); word-break:break-all; max-width:100%; opacity:0.7; }
  `]
})
export class CallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  theme: string = 'light';
  currentUrl: string = '';
  ngOnInit() {
    try {
      const saved = localStorage.getItem('orokanban-theme') as string | null;
      this.theme = saved === 'dark' || saved === 'light' ? saved : (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      document.documentElement.dataset['theme'] = this.theme;
    } catch {}
    try { this.currentUrl = window.location.href; } catch {}

    console.log('[callback] ngOnInit, url', window.location.href);
    // AppComponent ya hizo checkAuth() global y consume el ?code= una sola vez.
    // Este componente solo espera el resultado (evita doble canje → ID2010 "already been redeemed").
    const sub = this.oidc.isAuthenticated$.pipe(take(1)).subscribe(({ isAuthenticated }) => {
      console.log('[callback] isAuthenticated', isAuthenticated);
      if (isAuthenticated) {
        this.router.navigateByUrl('/');
        return;
      }
      // Si aún no está autenticado, espera un tick por si checkAuth de AppComponent está en vuelo
      setTimeout(() => {
        this.oidc.isAuthenticated$.pipe(take(1)).subscribe(({ isAuthenticated: stillAuth }) => {
          console.log('[callback] retry isAuthenticated', stillAuth);
          // No llamamos de nuevo a checkAuth() aquí para no re-canjear el code.
          // Si sigue sin autenticar, es un code ya canjeado (ID2010) o token inválido (ID2019) — volver a home y el guard re-disparará authorize si hace falta.
          this.router.navigateByUrl(stillAuth ? '/' : '/');
        });
      }, 800);
    });

    // Fallback por si isAuthenticated$ no emite (storage bloqueado)
    setTimeout(() => {
      if (window.location.pathname.includes('/auth/callback')) {
        console.warn('[callback] fallback timeout — navegando a home');
        sub.unsubscribe();
        this.router.navigateByUrl('/');
      }
    }, 4000);
  }
}
