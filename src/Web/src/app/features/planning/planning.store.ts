import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface PlanningState { milestones: any[]; }

export const PlanningStore = signalStore(
  withState<PlanningState>({ milestones: [] }),
  withRequestStatus(),
  withComputed(({ milestones }) => ({
    count: computed(()=> milestones().length)
  })),
  withMethods((store)=>{
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(pipe(switchMap(()=> {
        patchState(store, setPending());
        return http.get<any>('/api/planning/milestones').pipe(tapResponse({
          next: (res:any)=> {
            const items = res?.items ?? res ?? [];
            patchState(store, { milestones: Array.isArray(items)?items:[] }, setFulfilled());
          },
          error: (e:any)=> patchState(store, setError(e?.error?.detail ?? 'load failed'))
        }));
      })))
    };
  })
);
