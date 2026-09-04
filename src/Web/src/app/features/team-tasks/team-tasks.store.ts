import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface TeamTasksState { items: any[]; filter: string; q: string; }

export const TeamTasksStore = signalStore(
  withState<TeamTasksState>({ items: [], filter: 'all', q: '' }),
  withRequestStatus(),
  withComputed(({ items, filter, q }) => ({
    filtered: computed(() => {
      let v = items();
      const f = filter();
      if (f !== 'all') {
        const norm = (s: string) => s.toLowerCase().replace(/\s+/g,'');
        if (norm(f) === 'overdue') {
          v = v.filter((i:any)=> i.isOverdue || (i.dueDate && new Date(i.dueDate) < new Date() && (i.status||'').toLowerCase().replace(/\s+/g,'') !== 'completed'));
        } else {
          v = v.filter((i:any)=> (i.status||'').toLowerCase().replace(/\s+/g,'') === norm(f));
        }
      }
      if (q()) v = v.filter((i:any)=> (i.title||i.name||'').toLowerCase().includes(q().toLowerCase()));
      return v;
    }),
    count: computed(()=> items().length)
  })),
  withMethods((store)=>{
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(pipe(switchMap(()=> {
        patchState(store, setPending());
        return http.get<any>('/api/team-tasks?page=1&pageSize=20').pipe(tapResponse({
          next: (res:any)=> {
            const items = res?.items ?? res ?? [];
            patchState(store, { items: Array.isArray(items)?items:[] }, setFulfilled());
          },
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'load failed'))
        }));
      }))),
      setFilter: (f:string)=> patchState(store, { filter:f }),
      setQ: (q:string)=> patchState(store, { q })
    };
  })
);
