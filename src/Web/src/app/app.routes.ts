import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { CallbackComponent } from './core/auth/callback.component';
import { LogoutCallbackComponent } from './core/auth/logout-callback.component';

export const routes: Routes = [
  { path: '', canActivate: [authGuard], loadComponent: () => import('./features/home/home.component').then(m => m.HomeComponent) },
  { path: 'auth/callback', component: CallbackComponent },
  { path: 'auth/logout-callback', component: LogoutCallbackComponent },
  { path: '**', redirectTo: '' },
];
