import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface WorkItemDetail { id: string; title: string; progress: number; subtasks: { id: string; done: boolean }[]; metrics: any[]; }

export const WorkItemDetailStore = signalStore(
  withState<{ item: WorkItemDetail | null }>({ item: null }),
  withRequestStatus(),
  withComputed(({ item }) => ({
    progressExplanation: computed(() => {
      const i = item();
      if (!i) return null;
      const done = i.subtasks.filter(s => s.done).length;
      return { percent: i.progress, breakdown: `subtasks ${done}/${i.subtasks.length}, metrics ${i.metrics.length}` };
    }),
    isLoaded: computed(() => !!item())
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      async load(id: string) {
        patchState(store, setPending());
        try {
          const data = await http.get<WorkItemDetail>(`/api/work-items/${id}/detail`).toPromise() as WorkItemDetail;
          patchState(store, { item: data }, setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'load failed'));
        }
      }
    };
  })
);
