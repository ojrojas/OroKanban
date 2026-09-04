import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap, tap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface MyTask { id: string; title: string; status: string; projectId: string; assigneeId: string; }

interface MyTasksState { items: MyTask[]; filter: string; q: string; }

export const MyTasksStore = signalStore(
  withState<MyTasksState>({ items: [], filter: 'all', q: '' }),
  withRequestStatus(),
  withComputed(({ items, filter, q }) => ({
    filtered: computed(() => {
      let v = items();
      const f = filter();
      if (f !== 'all') {
        const norm = (s: string) => s.toLowerCase().replace(/\s+/g, '');
        const nf = norm(f);
        v = v.filter(i => {
          const s = norm(i.status || '');
          // Handle InProgress vs In Progress, Blocked exact, and isOverdue derived
          if (nf === 'overdue') return (i as any).isOverdue || (i as any).dueDate && new Date((i as any).dueDate) < new Date() && s !== 'completed';
          return s === nf;
        });
      }
      if (q()) v = v.filter(i => i.title.toLowerCase().includes(q().toLowerCase()) || (i.projectId||'').toLowerCase().includes(q().toLowerCase()));
      return v;
    }),
    count: computed(() => items().length)
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(pipe(
        switchMap(() => {
          patchState(store, setPending());
          return http.get<any>('/api/work-items?assignee=me&page=1&pageSize=20').pipe(
            tapResponse({
              next: (res: any) => {
                const items = res?.items ?? res ?? [];
                patchState(store, { items: Array.isArray(items) ? items : [] }, setFulfilled());
              },
              error: (e: any) => patchState(store, setError(e?.error?.detail ?? 'load failed'))
            })
          );
        })
      )),
      setFilter: (f: string) => patchState(store, { filter: f }),
      setQ: (q: string) => patchState(store, { q })
    };
  })
);
