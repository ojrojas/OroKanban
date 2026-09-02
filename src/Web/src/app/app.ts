import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  templateUrl: './app.html',
})
export class App implements OnInit {
  private oidc = inject(OidcSecurityService);

  ngOnInit(): void {
    // Maneja el callback (?code=) y restaura sesión desde storage en cada reload.
    // Si no hay sesión, el authGuard disparará oidc.authorize() y redirigirá al IdP.
    this.oidc.checkAuth().subscribe({
      next: ({ isAuthenticated }) => console.log('[App] checkAuth isAuthenticated', isAuthenticated),
      error: (err) => console.error('[App] checkAuth error', err),
    });
  }
}
