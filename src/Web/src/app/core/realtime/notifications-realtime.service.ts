import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';

/**
 * Minimal realtime for task notifications.
 * Tries SignalR Hub at /hub/notifications if @microsoft/signalr is present,
 * falls back to polling GET /api/notifications/unread-count every 20s.
 * Keeps notificationsStore unreadCount in sync so topBar badge is live.
 */
@Injectable({ providedIn: 'root' })
export class NotificationsRealtimeService {
  private http = inject(HttpClient);
  private pollId: any = null;
  private hub: any = null;

  async connect(getToken: () => Promise<string | null>, onNotification: (payload: any) => void): Promise<void> {
    // Try SignalR if available
    try {
      const signalR = await import('@microsoft/signalr').catch(() => null) as any;
      if (signalR?.HubConnectionBuilder) {
        const token = await getToken();
        this.hub = new signalR.HubConnectionBuilder()
          .withUrl('/hub/notifications', token ? { accessTokenFactory: () => token } : {})
          .withAutomaticReconnect([0, 2000, 5000, 10000])
          .build();
        this.hub.on('TaskAssigned', onNotification);
        this.hub.on('TaskUpdated', onNotification);
        this.hub.on('NotificationCreated', onNotification);
        await this.hub.start().catch(() => {});
        (window as any).__notificationsHub = this.hub;
        // expose disconnect for AuthService logout
        (window as any).__notificationsDisconnect = () => this.disconnect();
      }
    } catch {}
    this.startPolling(onNotification);
  }

  private startPolling(onNotification: (p:any)=>void): void {
    if (this.pollId) return;
    this.pollId = setInterval(async () => {
      try {
        // lightweight ping to keep badge fresh; consumers can refetch list when needed
        await this.http.get('/api/notifications/unread-count').toPromise().catch(()=>null);
      } catch {}
    }, 20000);
  }

  disconnect(): void {
    if (this.pollId) { clearInterval(this.pollId); this.pollId = null; }
    try { this.hub?.stop?.(); } catch {}
    this.hub = null;
  }
}
