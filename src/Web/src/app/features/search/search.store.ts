import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface SearchState { results: any[]; q: string; type: string; }

export const SearchStore = signalStore(
  withState<SearchState>({ results: [], q: '', type: 'all' }),
  withRequestStatus(),
  withComputed(({ results })=> ({ count: computed(()=> results().length) })),
  withMethods((store)=>{
    const http = inject(HttpClient);
    return {
      search: rxMethod<string>(pipe(switchMap((q:string)=> {
        patchState(store, { q }, setPending());
        if (!q) { patchState(store, { results: [] }, setFulfilled()); return pipe(()=>{}) as any; }
        return http.get<any>('/api/search', { params: { q, type: (store as any).type() } }).pipe(tapResponse({
          next: (res:any)=> {
            const items = res?.items ?? res ?? [];
            patchState(store, { results: Array.isArray(items)?items:[] }, setFulfilled());
          },
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'search failed'))
        }));
      }))),
      setType: (t:string)=> patchState(store, { type:t })
    };
  })
);
