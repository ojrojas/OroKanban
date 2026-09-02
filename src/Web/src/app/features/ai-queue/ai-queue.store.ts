import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface AiQueueState { items: any[]; }

export const AiQueueStore = signalStore(
  withState<AiQueueState>({ items: [] }),
  withRequestStatus(),
  withComputed(({ items })=> ({ pendingCount: computed(()=> items().filter((i:any)=> i.status==='Pending Review').length) })),
  withMethods((store)=>{
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(pipe(switchMap(()=> {
        patchState(store, setPending());
        return http.get<any>('/api/ai-queue').pipe(tapResponse({
          next: (res:any)=> {
            const items = res?.items ?? res ?? [];
            patchState(store, { items: Array.isArray(items)?items:[] }, setFulfilled());
          },
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'load failed'))
        }));
      }))),
      approve: rxMethod<string>(pipe(switchMap((id:string)=> {
        return http.post<any>(`/api/ai-queue/${id}/approve`, {}).pipe(tapResponse({
          next: ()=> (store as any).load(),
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'approve failed'))
        }));
      })))
    };
  })
);
