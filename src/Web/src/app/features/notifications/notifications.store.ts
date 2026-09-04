import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { withEntities, setAllEntities } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
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
      load: rxMethod<void>(
        pipe(
          switchMap(() => {
            patchState(store, setPending());
            return http.get<{ items: Notification[] }>('/api/notifications?page=1&pageSize=20').pipe(
              tapResponse({
                next: (res: any) => patchState(store, setAllEntities(res?.items ?? res ?? []), setFulfilled()),
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'load failed'))
              })
            );
          })
        )
      ),
      markRead: rxMethod<string>(
        pipe(
          switchMap((id) => {
            patchState(store, setPending());
            return http.post(`/api/notifications/${id}/read`, {}).pipe(
              tapResponse({
                next: () => {
                  const updated = store.entities().map(n => n.id === id ? { ...n, readAt: new Date().toISOString() } : n);
                  patchState(store, setAllEntities(updated as any), setFulfilled());
                },
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'markRead failed'))
              })
            );
          })
        )
      )
    };
  })
);
