import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App implements OnInit {
  protected readonly title = signal('orokanban-web');
  private oidc = inject(OidcSecurityService);

  ngOnInit(): void {
    // Maneja el callback (?code=) y restaura sesión desde storage en cada reload.
    // Si no hay sesión, el authGuard de '/' disparará oidc.authorize() y redirigirá al IdP.
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated }) => console.log('[App] checkAuth isAuthenticated', isAuthenticated),
      error: (err) => console.error('[App] checkAuth error', err),
    });
  }
}
