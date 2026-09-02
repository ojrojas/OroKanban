import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
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
      async load() {
        patchState(store, setPending());
        try {
          const data = await http.get<DashboardKpi[]>('/api/dashboard/kpis').toPromise() as DashboardKpi[];
          patchState(store, { kpis: data ?? [] }, setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'load failed'));
        }
      }
    };
  })
);
