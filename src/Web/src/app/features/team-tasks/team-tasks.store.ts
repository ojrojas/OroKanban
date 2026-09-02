import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface TeamTasksState { items: any[]; filter: string; }

export const TeamTasksStore = signalStore(
  withState<TeamTasksState>({ items: [], filter: 'all' }),
  withRequestStatus(),
  withComputed(({ items, filter }) => ({
    filtered: computed(() => filter()==='all' ? items() : items().filter((i:any)=> i.status===filter())),
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
      setFilter: (f:string)=> patchState(store, { filter:f })
    };
  })
);
