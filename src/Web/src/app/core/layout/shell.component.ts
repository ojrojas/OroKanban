import { Component, inject, signal, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, RouterLink, RouterLinkActive, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { HasPermissionDirective } from '../../shared/pipes/has-permission.directive';
import { ThemeService } from '../theme/theme.service';
import { AuthService } from '../auth/auth.service';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { HttpClient } from '@angular/common/http';
import { NotificationsRealtimeService } from '../realtime/notifications-realtime.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, HasPermissionDirective, FormsModule],
  template: `
    <div class="app-shell">
      @if (sidebarOpen()) {
        <div class="sidebar-backdrop" (click)="sidebarOpen.set(false)"></div>
      }
      <aside class="sidebar" [class.open]="sidebarOpen()">
        <div class="sidebar__brand">
          <div class="brand-mark">OK</div>
          <span class="brand-name">OroKanban</span>
        </div>

        <nav class="sidebar__nav" (click)="sidebarOpen.set(false)">
          <a routerLink="/dashboard" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/></svg>
            <span>Dashboard</span>
          </a>
          <a routerLink="/projects" routerLinkActive="active" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M3 7a2 2 0 0 1 2-2h4l2 2h6a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7z"/></svg>
            <span>Projects</span>
          </a>
          <a routerLink="/kanban" routerLinkActive="active" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><rect x="3" y="3" width="6" height="18" rx="1"/><rect x="10.5" y="3" width="6" height="10" rx="1"/></svg>
            <span>Kanban</span>
          </a>
          <a routerLink="/my-tasks" routerLinkActive="active" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/></svg>
            <span>My Tasks</span>
          </a>
          <a routerLink="/team-tasks" routerLinkActive="active" class="nav-item" *hasPermission="'team.read'">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
            <span>Team Tasks</span>
          </a>
          <a routerLink="/planning" routerLinkActive="active" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4"/><path d="M8 2v4"/><path d="M3 10h18"/></svg>
            <span>Planning</span>
          </a>
          <a routerLink="/documents" routerLinkActive="active" class="nav-item">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>
            <span>Documents</span>
          </a>
          <a routerLink="/organization" routerLinkActive="active" class="nav-item" *hasPermission="'organization.manage'">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M3 21h18"/><path d="M5 21V7a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v14"/><path d="M9 21v-6h6v6"/></svg>
            <span>Organization</span>
          </a>
          <a routerLink="/ai-queue" routerLinkActive="active" class="nav-item" *hasPermission="'ai.review'">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M12 8V4H8"/><rect x="4" y="12" width="8" height="8" rx="2"/><path d="M16 16h4v-4"/><path d="M20 12V8a2 2 0 0 0-2-2h-4"/></svg>
            <span>AI Queue</span>
          </a>
          <a routerLink="/audit" routerLinkActive="active" class="nav-item" *hasPermission="'audit.read'">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><path d="M9 15h6"/><path d="M9 18h6"/></svg>
            <span>Audit</span>
          </a>
          <a routerLink="/admin" routerLinkActive="active" class="nav-item" *hasPermission="'admin'">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M12 20a8 8 0 1 0 0-16 8 8 0 0 0 0 16z"/><path d="M12 14a2 2 0 1 0 0-4 2 2 0 0 0 0 4z"/></svg>
            <span>Administration</span>
          </a>
        </nav>

        <div class="sidebar__footer">
          <button class="fab-tier2" (click)="theme.toggle()" [attr.aria-label]="theme.theme()==='dark' ? 'Light mode' : 'Dark mode'" [title]="theme.theme()==='dark' ? 'Light mode' : 'Dark mode'">
            @if (theme.theme()==='dark') {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><circle cx="12" cy="12" r="5"/><path d="M12 1v2"/><path d="M12 21v2"/><path d="M4.22 4.22l1.42 1.42"/><path d="M18.36 18.36l1.42 1.42"/><path d="M1 12h2"/><path d="M21 12h2"/><path d="M4.22 19.78l1.42-1.42"/><path d="M18.36 5.64l1.42-1.42"/></svg>
            } @else {
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>
            }
          </button>
          <button class="fab-tier2" aria-label="Settings" title="Settings">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06A1.65 1.65 0 0 0 15 19.4a1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06A1.65 1.65 0 0 0 4.6 15a1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06A1.65 1.65 0 0 0 9 4.6a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06A1.65 1.65 0 0 0 19.4 9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>
          </button>
          <button class="logout-btn" (click)="auth.logout()" aria-label="Logout" title="Logout">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><polyline points="16 17 21 12 16 7"/><line x1="21" y1="12" x2="9" y2="12"/></svg>
            <span>Logout</span>
          </button>
        </div>
      </aside>

      <div class="main-column">
        <header class="top-bar">
          <div class="top-bar__left">
            <button class="hamburger" (click)="sidebarOpen.set(!sidebarOpen())" aria-label="Menu">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M3 6h18"/><path d="M3 12h18"/><path d="M3 18h18"/></svg>
            </button>
            <div class="search-field tier-1">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#A9A9A9" stroke-width="1.75"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.35-4.35"/></svg>
              <input placeholder="Search anything..." aria-label="Search" [(ngModel)]="searchQuery" (keydown.enter)="onSearch()" />
            </div>
          </div>
          <div class="top-bar__right">
            <button class="btn-primary" (click)="router.navigate(['/projects'])">Create</button>

            <!-- Theme quick toggle -->
            <button class="icon-btn tier-1" (click)="theme.toggle()" [attr.aria-label]="theme.theme()==='dark' ? 'Light' : 'Dark'" title="Change theme">
              @if (theme.theme()==='dark') {
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><circle cx="12" cy="12" r="5"/><path d="M12 1v2"/><path d="M12 21v2"/><path d="M4.22 4.22l1.42 1.42"/><path d="M18.36 18.36l1.42 1.42"/><path d="M1 12h2"/><path d="M21 12h2"/><path d="M4.22 19.78l1.42-1.42"/><path d="M18.36 5.64l1.42-1.42"/></svg>
              } @else {
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"/></svg>
              }
            </button>

            <!-- Notifications zone — bell with badge + dropdown -->
            <div class="notif-wrap">
              <button class="icon-btn tier-1 notif-bell" (click)="toggleNotif()" aria-label="Notifications">
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75"><path d="M6 8a6 6 0 0 1 12 0c0 7-6 9-6 9s-6-2-6-9"/><path d="M10 21a2 2 0 0 0 4 0"/></svg>
                @if (unreadCount() > 0) {
                  <span class="notif-badge">{{ unreadCount() > 99 ? '99+' : unreadCount() }}</span>
                }
              </button>
              @if (showNotif()) {
                <div class="notif-dropdown tier-2">
                  <div class="notif-header">
                    <strong>Notifications</strong>
                    <button class="link-btn" (click)="markAllRead()">Mark all read</button>
                  </div>
                  @if (notifications().length === 0) {
                    <div class="notif-empty">No notifications</div>
                  } @else {
                    @for (n of notifications(); track n.id) {
                      <div class="notif-item" (click)="openNotification(n)">
                        <div class="notif-title">{{ n.title }}</div>
                        <div class="notif-body">{{ n.body }}</div>
                        <div class="notif-time">{{ n.createdAt | date:'short' }}</div>
                      </div>
                    }
                  }
                  <a routerLink="/notifications" class="notif-viewall" (click)="showNotif.set(false)">View all →</a>
                </div>
              }
            </div>

            <img class="avatar" src="https://i.pravatar.cc/100?img=32" alt="avatar" (click)="auth.logout()" style="cursor:pointer" title="Logout" />
          </div>
        </header>

        <main class="page-content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; background: var(--bg); }
    .app-shell { display: flex; min-height: 100vh; background: var(--bg); }
    .main-column { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: var(--gap-region); padding: var(--outer); max-width: 100%; }
    .page-content { display: flex; flex-direction: column; gap: var(--gap-widget); flex:1; min-height:0; }
    .sidebar { width: var(--nav-width); flex-shrink: 0; background: var(--bg); display: flex; flex-direction: column; padding: var(--outer); gap: 24px; }
    .sidebar__brand { display: flex; align-items: center; gap: 10px; padding: 4px 8px 8px; }
    .brand-mark { width: 32px; height: 32px; border-radius: 10px; background: var(--black); color: var(--on-black); display: grid; place-items: center; font-weight: 700; font-size: 12px; }
    .brand-name { font-weight: 700; font-size: 15px; color: var(--text-primary); letter-spacing: -0.02em; }
    .sidebar__nav { display: flex; flex-direction: column; gap: 4px; flex: 1; }
    .nav-item { display: flex; align-items: center; gap: 10px; padding: 11px 12px; border-radius: var(--radius-pill); color: var(--text-secondary); text-decoration: none; font-size: 13px; font-weight: 500; background: transparent; transition: background 200ms, color 200ms, box-shadow 200ms; }
    .nav-item svg { color: var(--text-secondary); flex-shrink: 0; }
    .nav-item:hover:not(.active) { background: var(--flat-bg); color: var(--text-primary); }
    .nav-item.active { background: var(--card-bg); box-shadow: var(--shadow-card); color: var(--text-primary); font-weight: 600; }
    .sidebar__footer { display: flex; gap: 8px; padding-top: 12px; border-top: 1px solid var(--border); align-items: center; flex-wrap: wrap; }
    .fab-tier2 { width: 36px; height: 36px; border-radius: 50%; background: var(--card-bg); border: none; box-shadow: var(--shadow-card); display: grid; place-items: center; color: var(--text-secondary); cursor: pointer; transition: box-shadow 200ms, transform 200ms; }
    .fab-tier2:hover { box-shadow: var(--shadow-hover); transform: translateY(-1px); color: var(--text-primary); }
    .logout-btn { display: flex; align-items: center; gap: 6px; margin-left: auto; padding: 8px 12px; border-radius: var(--radius-pill); background: var(--card-bg); border: 1px solid var(--border); color: var(--text-secondary); font-size: 12px; font-weight: 600; cursor: pointer; }
    .logout-btn:hover { background: var(--flat-bg); color: var(--text-primary); }
    .top-bar { display: flex; align-items: center; justify-content: space-between; gap: 16px; flex-wrap: wrap; }
    .top-bar__left { flex: 1; min-width: 260px; max-width: 520px; display:flex; align-items:center; gap:10px; }
    .top-bar__right { display: flex; align-items: center; gap: 12px; }
    .search-field { display: flex; align-items: center; gap: 10px; padding: 10px 16px; border-radius: var(--radius-input); background: var(--flat-bg); border: 1px solid var(--border); }
    .search-field input { flex: 1; border: none; outline: none; background: transparent; font-size: 13px; color: var(--text-primary); }
    .search-field input::placeholder { color: var(--text-muted); }
    .btn-primary { background: var(--black); color: var(--on-black); border: none; border-radius: var(--radius-button); padding: 10px 20px; font-size: 13px; font-weight: 600; cursor: pointer; }
    .icon-btn { width: 40px; height: 40px; border-radius: 50%; display: grid; place-items: center; background: var(--flat-bg); border: 1px solid var(--border); color: var(--text-secondary); cursor: pointer; position: relative; }
    .avatar { width: 36px; height: 36px; border-radius: 50%; object-fit: cover; }
    .notif-wrap { position: relative; }
    .notif-badge { position: absolute; top: -4px; right: -4px; background: var(--red-bg); color: var(--red-text); font-size: 10px; font-weight: 700; min-width: 18px; height: 18px; border-radius: 999px; display: grid; place-items: center; padding: 0 4px; border: 1px solid var(--card-bg); }
    .notif-dropdown { position: absolute; top: 48px; right: 0; width: 360px; max-height: 420px; overflow: auto; background: var(--card-bg); border-radius: var(--radius-card); box-shadow: var(--shadow-hover); padding: 16px; z-index: 50; }
    .notif-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; font-size: 13px; }
    .link-btn { background: none; border: none; color: var(--text-secondary); font-size: 12px; cursor: pointer; }
    .notif-empty { font-size: 13px; color: var(--text-muted); padding: 16px 0; text-align: center; }
    .notif-item { padding: 10px 12px; border-top: 1px solid var(--border); cursor: pointer; }
    .notif-item:hover { background: var(--flat-bg); }
    .notif-title { font-weight: 600; font-size: 13px; color: var(--text-primary); }
    .notif-body { font-size: 12px; color: var(--text-secondary); margin-top: 2px; }
    .notif-time { font-size: 11px; color: var(--text-muted); margin-top: 4px; }
    .notif-viewall { display: block; text-align: center; font-size: 12px; color: var(--text-secondary); margin-top: 12px; text-decoration: none; }
    .notif-viewall:hover { color: var(--text-primary); }
    .hamburger { display:none; }
    .sidebar-backdrop { display:none; }
    @media (max-width: 900px) {
      .hamburger { display:grid; width:40px; height:40px; border-radius:50%; border:1px solid var(--border); background:var(--flat-bg); place-items:center; color:var(--text-secondary); cursor:pointer; flex-shrink:0; }
      .sidebar { position:fixed; left:0; top:0; bottom:0; z-index:40; transform: translateX(-100%); transition: transform 200ms ease; box-shadow: var(--shadow-hover); overflow:auto; }
      .sidebar.open { transform: translateX(0); }
      .sidebar-backdrop { display:block; position:fixed; inset:0; background: rgba(0,0,0,.2); z-index:30; }
      .main-column { padding: 16px; gap: 16px; }
      .notif-dropdown { width: 300px; right: -40px; }
      .top-bar__left { display:flex; align-items:center; gap:10px; }
    }
  `]
})
export class ShellComponent implements OnInit, OnDestroy {
  theme = inject(ThemeService);
  auth = inject(AuthService);
  router = inject(Router);
  private http = inject(HttpClient);
  private oidc = inject(OidcSecurityService);
  private realtime = inject(NotificationsRealtimeService);

  sidebarOpen = signal(false);
  searchQuery = '';
  showNotif = signal(false);
  unreadCount = signal(0);
  notifications = signal<any[]>([]);
  private poll: any = null;

  ngOnInit(): void {
    // Only poll when authenticated to avoid 401/500 spam before login
    this.oidc.isAuthenticated$.subscribe(({ isAuthenticated }: any) => {
      if (!isAuthenticated) {
        if (this.poll) { clearInterval(this.poll); this.poll = null; }
        this.unreadCount.set(0);
        return;
      }
      this.refreshNotif();
      if (!this.poll) this.poll = setInterval(()=> this.refreshNotif(), 60000);
      this.realtime.connect(
        async () => {
          try {
            const token: any = await (this.oidc.getAccessToken() as any).toPromise?.() ?? null;
            return typeof token === 'string' ? token : token?.accessToken ?? null;
          } catch { return null; }
        },
        () => this.refreshNotif()
      ).catch(()=>{});
    });
  }

  toggleNotif(): void {
    this.showNotif.update(v=>!v);
    if (this.showNotif()) this.refreshNotif();
  }

  private refreshNotif(): void {
    this.http.get<any>('/api/notifications?pagesize=5').subscribe({
      next: (res: any) => {
        const items = res?.items ?? res ?? [];
        this.notifications.set(Array.isArray(items)? items : []);
        const unread = items.filter((n:any)=> !n.isRead).length;
        if (typeof unread === 'number') this.unreadCount.set(unread);
      },
      error: ()=> {}
    });
    this.http.get<any>('/api/notifications/unread-count').subscribe({
      next: (res:any)=> {
        const c = res?.count ?? res?.unreadCount ?? 0;
        if (typeof c === 'number') this.unreadCount.set(c);
      }, error: ()=> {}
    });
  }

  markAllRead(): void {
    this.http.post('/api/notifications/mark-all-read', {}).subscribe({ next: ()=> this.refreshNotif() });
  }

  openNotification(n:any): void {
    this.showNotif.set(false);
    if (n.link) this.router.navigateByUrl(n.link);
    else if (n.id) this.http.post('/api/notifications/'+n.id+'/read', {}).subscribe();
  }

  ngOnDestroy(): void {
    if (this.poll) { clearInterval(this.poll); this.poll = null; }
    try { this.realtime.disconnect(); } catch {}
  }

  onSearch(): void {
    const q = this.searchQuery.trim();
    this.router.navigate(['/search'], { queryParams: q ? { q } : {} });
  }
}
