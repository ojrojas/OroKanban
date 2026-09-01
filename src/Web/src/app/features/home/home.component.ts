import { Component, OnInit, inject } from '@angular/core';
import { AsyncPipe, JsonPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/auth/auth.service';
import { take } from 'rxjs';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [AsyncPipe, JsonPipe],
  template: `
    <div style="padding:2rem; max-width:800px; margin:0 auto;">
      <h1>OroKanban — Bienvenido</h1>
      @if (authService.isAuthenticated$ | async; as result) {
        @if (result.isAuthenticated) {
          <p>Autenticado ✓</p>
          <pre style="background:#f5f5f5; padding:1rem; overflow:auto; font-size:0.85rem;">{{ (authService.userData$ | async) | json }}</pre>
          @if (helloResponse) {
            <pre style="background:#e8f5e9; padding:1rem; overflow:auto; font-size:0.85rem; margin-top:1rem;">GET /api/hello → {{ helloResponse | json }}</pre>
          }
          @if (helloError) {
            <pre style="background:#ffebee; padding:1rem; overflow:auto; font-size:0.85rem; color:#c62828;">GET /api/hello error → {{ helloError | json }}</pre>
          }
          <p><button (click)="authService.logout()">Cerrar sesión</button></p>
          <p><a href="/api/hello" target="_blank">Probar GET /api/hello (requiere Bearer)</a> — el interceptor ya adjunta el token.</p>
        } @else {
          <p>No autenticado — redirigiendo a login...</p>
          <p><button (click)="authService.login()">Iniciar sesión</button></p>
        }
      } @else {
        <p>Cargando...</p>
      }
    </div>
  `,
})
export class HomeComponent implements OnInit {
  authService = inject(AuthService);
  private http = inject(HttpClient);
  helloResponse: unknown = null;
  helloError: unknown = null;

  ngOnInit(): void {
    // Cuando el usuario ya está autenticado al llegar al home, dispara GET /api/hello para validar la integración
    this.authService.isAuthenticated$.pipe(take(1)).subscribe(({ isAuthenticated }) => {
      if (!isAuthenticated) return;
      console.log('[Home] Autenticado — enviando GET /api/hello para validar autenticación');
      this.http.get('/api/hello', { headers: { 'X-Skip-Auth-Redirect': 'true' } }).subscribe({
        next: (res) => {
          this.helloResponse = res;
          console.log('[Home] GET /api/hello ✓', res);
        },
        error: (err) => {
          this.helloError = err?.error ?? err;
          console.error('[Home] GET /api/hello ✗', err);
          // No disparamos otro authorize aquí — el errorInterceptor ya lo haría con throttle, y lo hemos excluido con X-Skip-Auth-Redirect
        },
      });
    });

    // También escucha cambios futuros (ej. tras silentRenew) y revalida
    this.authService.isAuthenticated$.subscribe(({ isAuthenticated }) => {
      if (isAuthenticated && !this.helloResponse && !this.helloError) {
        // ya disparado arriba; este es para navegaciones posteriores sin recargar
      } else if (!isAuthenticated) {
        this.helloResponse = null;
        this.helloError = null;
      }
    });
  }
}
