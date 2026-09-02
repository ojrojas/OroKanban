import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { NotificationsStore } from './notifications.store';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, RouterLink],
  providers: [NotificationsStore],
  template: `
    <div class="page-header">
      <h1 class="page-header__title">Notifications</h1>
      <p class="page-header__subtitle">InApp notifications — unread {{ store.unreadCount() }}</p>
    </div>

    @if (store.isPending()) {
      <div class="tier-2" style="padding:24px;"><div class="skeleton"></div></div>
    } @else if (store.error()) {
      <div class="tier-2" style="padding:16px; display:flex; justify-content:space-between;">
        <span style="color:var(--red-text); font-size:13px;">{{ store.error() }}</span>
        <button class="btn-secondary" (click)="store.load()">Retry</button>
      </div>
    }     @else {
      <div class="tier-2" style="padding:0; overflow:hidden;">
        <div style="padding:16px 24px; display:flex; justify-content:space-between; align-items:center; border-bottom:1px solid var(--border);">
          <h3 style="margin:0; font-size:14px; font-weight:700;">Inbox</h3>
          <button class="pill" (click)="store.load()">Refresh</button>
        </div>
        @for (n of store.entities(); track n.id) {
          <div class="row" (click)="store.markRead(n.id)">
            <div class="thumb" [class.unread]="!n.readAt"></div>
            <div style="flex:1">
              <div style="font-weight:600; font-size:13px;">{{ n.title }}</div>
              <div style="font-size:11px; color:var(--text-muted);">{{ n.readAt ? (n.readAt | date:'short') : 'Unread' }}</div>
            </div>
            @if (!n.readAt) { <span class="badge-ok">Unread</span> } @else { <span class="badge">Read</span> }
          </div>
        }
        @if (store.entities().length===0) {
          <div style="padding:32px; text-align:center; color:var(--text-muted); font-size:13px;">No notifications — you'll be notified here when tasks are assigned</div>
        }
      </div>
    }
  `,
  styles: [`
    .badge { background:var(--flat-bg); border:1px solid var(--border); padding:4px 8px; border-radius:999px; font-size:11px; }
    .badge-ok { background:var(--green-bg); color:var(--green-text); padding:4px 8px; border-radius:999px; font-size:11px; }
    .row { display:flex; gap:12px; padding:12px 24px; border-top:1px solid var(--border); align-items:center; cursor:pointer; }
    .row:hover { background: var(--flat-bg); }
    .thumb { width:36px; height:36px; border-radius:12px; background:var(--border); }
    .thumb.unread { background: var(--green-bg); border:1px solid var(--green-text); }
    .skeleton { height:14px; background:var(--border); border-radius:6px; margin:8px 0; }
    .pill { padding:6px 12px; border-radius:999px; font-size:12px; background:var(--flat-bg); border:1px solid var(--border); cursor:pointer; }
    .btn-secondary { background:var(--flat-bg); border:1px solid var(--border); border-radius:999px; padding:6px 12px; cursor:pointer; }
  `]
})
export class NotificationsPage implements OnInit {
  store = inject(NotificationsStore);
  ngOnInit(): void { this.store.load(); }
}
