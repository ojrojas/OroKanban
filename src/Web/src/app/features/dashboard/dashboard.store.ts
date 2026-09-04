import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface DashboardKpi { key: string; value: number; delta?: number; link: string; }

interface DashboardState { kpis: DashboardKpi[]; }

export const DashboardStore = signalStore(
  withState<DashboardState>({ kpis: [] }),
  withRequestStatus(),
  withComputed(({ kpis }) => ({
    overdue: computed(() => kpis().find(k => k.key === 'overdue')?.value ?? 0),
    blocked: computed(() => kpis().find(k => k.key === 'blocked')?.value ?? 0),
    totalProjects: computed(() => kpis().find(k => k.key === 'myProjects')?.value ?? 0),
    hasKpis: computed(() => kpis().length > 0)
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      load: rxMethod<void>(
        pipe(
          switchMap(() => {
            patchState(store, setPending());
            return http.get<any>('/api/dashboard/kpis').pipe(
              tapResponse({
                next: (res: any) => {
                  const data = res?.items ?? res;
                  const kpis = Array.isArray(data) ? (data as DashboardKpi[]) : [];
                  patchState(store, { kpis }, setFulfilled());
                },
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'load failed'))
              })
            );
          })
        )
      )
    };
  })
);
