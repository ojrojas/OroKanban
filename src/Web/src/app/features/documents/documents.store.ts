import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface DocumentsState { items: any[]; filter: string; q: string; page: number; total: number; }

export const DocumentsStore = signalStore(
  withState<DocumentsState>({ items: [], filter: 'all', q: '', page: 1, total: 0 }),
  withRequestStatus(),
  withComputed(({ items })=> ({ count: computed(()=> items().length) })),
  withMethods((store)=>{
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(pipe(switchMap(()=> {
        const s = store as any;
        patchState(store, setPending());
        const params: any = { page: s.page(), pageSize: 20 };
        if (s.filter() !== 'all') params.filter = s.filter();
        if (s.q()) params.q = s.q();
        return http.get<any>('/api/documents', { params }).pipe(tapResponse({
          next: (res:any)=> {
            patchState(store, { items: res?.items ?? [], total: res?.total ?? 0 }, setFulfilled());
          },
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'load failed'))
        }));
      }))),
      setFilter: (f:string)=> patchState(store, { filter:f }),
      setQ: (q:string)=> patchState(store, { q }),
      setPage: (p:number)=> patchState(store, { page:p })
    };
  })
);
