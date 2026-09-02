import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { withEntities, setAllEntities } from '@ngrx/signals/entities';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface Notification { id: string; title: string; readAt: string | null; }

export const NotificationsStore = signalStore(
  withEntities<Notification>(),
  withState<{ filter: string }>({ filter: '' }),
  withRequestStatus(),
  withComputed(({ entities }) => ({
    unreadCount: computed(() => entities().filter(n => !n.readAt).length),
    total: computed(() => entities().length)
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      setFilter(filter: string) { patchState(store, { filter }); },
      async load() {
        patchState(store, setPending());
        try {
          const res = await http.get<{ items: Notification[] }>('/api/notifications?page=1&pageSize=20').toPromise() as any;
          patchState(store, setAllEntities(res?.items ?? res ?? []), setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'load failed'));
        }
      },
      async markRead(id: string) {
        patchState(store, setPending());
        try {
          await http.post(`/api/notifications/${id}/read`, {}).toPromise();
          const updated = store.entities().map(n => n.id === id ? { ...n, readAt: new Date().toISOString() } : n);
          patchState(store, setAllEntities(updated as any), setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'markRead failed'));
        }
      }
    };
  })
);
