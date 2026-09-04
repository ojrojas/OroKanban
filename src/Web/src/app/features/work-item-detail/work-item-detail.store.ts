import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface WorkItemDetail { id: string; title: string; description?: string | null; status?: string; priority?: string; criticality?: string; dueDate?: string | null; progress: number; version?: number; updatedAt?: string; deliverables?: string[]; tags?: string[]; observations?: string | null; subtasks: { id: string; done: boolean }[]; metrics: any[]; [k:string]: any; }

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
      load: rxMethod<string>(
        pipe(
          switchMap((id) => {
            patchState(store, setPending());
            return http.get<WorkItemDetail>(`/api/work-items/${id}/detail`).pipe(
              tapResponse({
                next: (data) => patchState(store, { item: data }, setFulfilled()),
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'load failed'))
              })
            );
          })
        )
      )
    };
  })
);
