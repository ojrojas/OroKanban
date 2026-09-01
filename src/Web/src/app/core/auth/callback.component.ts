import { Component, OnInit, inject } from '@angular/core';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { take } from 'rxjs';

@Component({
  selector: 'app-callback',
  standalone: true,
  template: `
    <p>Autenticando...</p>
    <p style="font-size:0.8rem; opacity:0.6;">Si quedas aquí más de 3s, abre F12 → Console → [callback]</p>
  `,
})
export class CallbackComponent implements OnInit {
  private oidc = inject(OidcSecurityService);
  private router = inject(Router);
  ngOnInit() {
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
