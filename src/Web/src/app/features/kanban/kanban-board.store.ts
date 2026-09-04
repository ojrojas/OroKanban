import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

interface BoardItem { id: string; title: string; criticality: string; isOverdue: boolean; [k: string]: any; }
interface BoardColumn { status: string; statusId: number; count: number; items: BoardItem[]; }

interface KanbanState {
  columns: BoardColumn[];
  filters: Record<string, unknown>;
  projectId: string | null;
}

export const KanbanBoardStore = signalStore(
  withState<KanbanState>({ columns: [], filters: {}, projectId: null }),
  withRequestStatus(),
  withComputed(({ columns }) => ({
    overdueCount: computed(() => columns().flatMap((c) => c.items).filter((i) => i.isOverdue).length),
    totalCount: computed(() => columns().reduce((s, c) => s + c.count, 0)),
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      setProject(projectId: string) {
        patchState(store, { projectId });
      },
      setFilter(filters: Record<string, unknown>) {
        patchState(store, { filters });
      },
      loadBoard: rxMethod<string | void>(
        pipe(
          switchMap((projectId) => {
            const pid = (projectId as string) ?? store.projectId();
            if (!pid) return [] as any;
            patchState(store, setPending());
            return http.get<{ columns: BoardColumn[] }>(`/api/projects/${pid}/board`).pipe(
              tapResponse({
                next: (board: any) => patchState(store, { columns: board?.columns ?? [] }, setFulfilled()),
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'load failed'))
              })
            );
          })
        )
      ),
      dragDrop: rxMethod<{ workItemId: string; targetStatus: string; expectedVersion: number }>(
        pipe(
          switchMap(({ workItemId, targetStatus, expectedVersion }) => {
            const pid = store.projectId();
            if (!pid) return [] as any;
            patchState(store, setPending());
            return http
              .put(`/api/work-items/${workItemId}/status`, { targetStatus, expectedVersion }, { headers: { 'If-Match': `W/"${expectedVersion}"` } })
              .pipe(
                switchMap(() => http.get<{ columns: BoardColumn[] }>(`/api/projects/${pid}/board`)),
                tapResponse({
                  next: (board: any) => patchState(store, { columns: board?.columns ?? [] }, setFulfilled()),
                  error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'update failed'))
                })
              );
          })
        )
      ),
    };
  })
);
