import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { withEntities, setAllEntities, setEntity } from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { pipe, switchMap } from 'rxjs';
import { tapResponse } from '@ngrx/operators';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface Project { id: string; name: string; status: string; priority?: string; criticality?: string; description?: string | null; dueDate?: string | null; ownerId?: string; createdAt?: string; updatedAt?: string; }

export const ProjectsStore = signalStore(
  withEntities<Project>(),
  withState<{ filter: string; page: number; pageSize: number; total: number }>({ filter: '', page: 1, pageSize: 15, total: 0 }),
  withRequestStatus(),
  withComputed(({ entities, filter, page, pageSize, total }) => ({
    filtered: computed(() => {
      const q = filter().toLowerCase();
      if (!q) return entities();
      // support filtering by name OR status (for pills All/Active/Archived)
      return entities().filter(p => p.name.toLowerCase().includes(q) || p.status.toLowerCase().includes(q));
    }),
    total: computed(() => total() || entities().length),
    totalPages: computed(() => Math.max(1, Math.ceil((total() || entities().length) / (pageSize() as number)))),
    hasNext: computed(() => (page() as number) < Math.max(1, Math.ceil((total() || entities().length) / (pageSize() as number)))),
    hasPrev: computed(() => (page() as number) > 1)
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    const doLoad = (pageOverride?: number, pageSizeOverride?: number) => {
      patchState(store, setPending());
      const page = pageOverride ?? (store as any).page();
      const pageSize = pageSizeOverride ?? (store as any).pageSize();
      const q = (store as any).filter();
      const params: any = { page, pageSize };
      if (q) params.q = q;
      return http.get<any>('/api/projects', { params }).pipe(
        tapResponse({
          next: (res: any) => {
            const items = res?.items ?? res ?? [];
            const totalVal = res?.total ?? items.length;
            patchState(store, setAllEntities(items), { total: totalVal } as any, setFulfilled());
          },
          error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'load failed'))
        })
      );
    };
    return {
      setFilter(filter: string) { patchState(store, { filter, page: 1 }); },
      setPage(page: number) { patchState(store, { page: Math.max(1, page) }); },
      setPageSize(pageSize: number) {
        const clamped = [10,15,25,50,100].includes(pageSize) ? pageSize : 15;
        patchState(store, { pageSize: clamped, page: 1 });
      },
      nextPage() {
        const cur = (store as any).page();
        const tp = (store as any).totalPages();
        if (cur < tp) patchState(store, { page: cur + 1 });
      },
      prevPage() {
        const cur = (store as any).page();
        if (cur > 1) patchState(store, { page: cur - 1 });
      },
      load: rxMethod<void>(
        pipe(
          switchMap(() => doLoad())
        )
      ),
      reload: rxMethod<void>(
        pipe(switchMap(()=> doLoad()))
      ),
      create: rxMethod<Partial<Project>>(
        pipe(
          switchMap((project) => {
            patchState(store, setPending());
            return http.post<Project>('/api/projects', project).pipe(
              tapResponse({
                next: (created) => {
                  patchState(store, setEntity(created), setFulfilled());
                  // refresh current page to reflect total and ordering
                  doLoad().subscribe();
                },
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'create failed'))
              })
            );
          })
        )
      ),
      update: rxMethod<{ id: string; patch: Partial<Project> }>(
        pipe(
          switchMap(({ id, patch }) => {
            patchState(store, setPending());
            return http.put<Project>(`/api/projects/${id}`, patch).pipe(
              tapResponse({
                next: (updated) => {
                  patchState(store, setEntity(updated), setFulfilled());
                  doLoad().subscribe();
                },
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'update failed'))
              })
            );
          })
        )
      ),
      archive: rxMethod<string>(
        pipe(
          switchMap((id) => {
            patchState(store, setPending());
            return http.post<Project>(`/api/projects/${id}/archive`, {}).pipe(
              tapResponse({
                next: (updated) => {
                  patchState(store, setEntity(updated), setFulfilled());
                  doLoad().subscribe();
                },
                error: (e: any) => patchState(store, setError(e?.error?.detail ?? e?.message ?? 'archive failed'))
              })
            );
          })
        )
      )
    };
  })
);
