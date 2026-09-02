import { computed, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { withEntities, setAllEntities, setEntity, removeEntity } from '@ngrx/signals/entities';
import { setError, setFulfilled, setPending, withRequestStatus } from '../../shared/state/with-request-status';

export interface Project { id: string; name: string; status: string; }

export const ProjectsStore = signalStore(
  withEntities<Project>(),
  withState<{ filter: string; page: number; pageSize: number }>({ filter: '', page: 1, pageSize: 20 }),
  withRequestStatus(),
  withComputed(({ entities, filter }) => ({
    filtered: computed(() => {
      const q = filter().toLowerCase();
      return q ? entities().filter(p => p.name.toLowerCase().includes(q)) : entities();
    }),
    total: computed(() => entities().length)
  })),
  withMethods((store) => {
    const http = inject(HttpClient);
    return {
      setFilter(filter: string) { patchState(store, { filter }); },
      async load() {
        patchState(store, setPending());
        try {
          const res = await http.get<{ items: Project[] }>(`/api/projects?page=${store.page()}&pageSize=${store.pageSize()}&q=${store.filter()}`).toPromise() as any;
          patchState(store, setAllEntities(res?.items ?? res ?? []), setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'load failed'));
        }
      },
      async create(project: Partial<Project>) {
        patchState(store, setPending());
        try {
          const created = await http.post<Project>('/api/projects', project).toPromise() as Project;
          patchState(store, setEntity(created), setFulfilled());
        } catch (e: any) {
          patchState(store, setError(e?.message ?? 'create failed'));
        }
      }
    };
  })
);
