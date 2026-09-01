import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { setError, setFulfilled, setPending, withRequestStatus } from './with-request-status';

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
      async loadBoard(projectId?: string) {
        const pid = projectId ?? store.projectId();
        if (!pid) return;
        patchState(store, setPending());
        try {
          const board = await http
            .get<{ columns: BoardColumn[] }>(`/api/projects/${pid}/board`)
            .toPromise() as any;
          patchState(store, { columns: board?.columns ?? [] }, setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'load failed'));
        }
      },
      async dragDrop(workItemId: string, targetStatus: string, expectedVersion: number) {
        const pid = store.projectId();
        if (!pid) return;
        patchState(store, setPending());
        try {
          await http
            .post(`/api/workitems/${workItemId}/status`, { targetStatus, expectedVersion })
            .toPromise();
          const board = await http
            .get<{ columns: BoardColumn[] }>(`/api/projects/${pid}/board`)
            .toPromise() as any;
          patchState(store, { columns: board?.columns ?? [] }, setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'update failed'));
        }
      },
    };
  })
);
