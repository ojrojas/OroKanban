# Contract: State Stores (NgRx SignalStore)

**Skill**: `.agents/skills/ngrx-signal-store/SKILL.md` — todo estado de feature vive en `SignalStore`, nunca `BehaviorSubject`.

## Factory común

```ts
// shared/state/with-request-status.ts (del skill)
export type RequestStatus = 'idle'|'pending'|'fulfilled'|{error:string};
export function withRequestStatus(){ return signalStoreFeature(withState<{requestStatus:RequestStatus}>({requestStatus:'idle'}), withComputed(({requestStatus})=> ({isPending: computed(()=>requestStatus()==='pending')}))) }
```

## Store por feature (12)

```ts
// dashboard.store.ts
export const DashboardStore = signalStore(
  withState<{kpis: DashboardKPI[], loading: boolean, error: string|null}>({kpis:[], loading:false, error:null}),
  withComputed(({kpis})=> ({ overdue: computed(()=> kpis().find(k=> k.key==='overdue')?.value ?? 0) })),
  withMethods((store, api=inject(DashboardApi))=> ({
    load: rxMethod<void>(pipe(switchMap(()=> { patchState(store,{loading:true}); return api.getKpis().pipe(tapResponse({next: v=> patchState(store,{kpis:v, loading:false}), error: e=> patchState(store,{error: e.message, loading:false})}))}))
  })),
  withProps(()=> ({_api: inject(DashboardApi)})),
  withHooks({ onInit(s){ s.load(); }}),
  withRequestStatus()
);

// kanban.store.ts — con entities
export const KanbanStore = signalStore(
  withEntities<WorkItem>(),
  withState<{filter: {projectId: string|null}}>({filter:{projectId:null}}),
  withComputed(({entities, filter})=> ({ filtered: computed(()=> entities().filter(e=> !filter().projectId || e.projectId===filter().projectId)) })),
  withMethods((store, api=inject(KanbanApi))=> ({
    load: rxMethod<void>(pipe(switchMap(()=> api.list().pipe(tapResponse({next: v=> patchState(store, setAllEntities(v.items)), error: e=> patchState(store,{error:e})})))),
    move: rxMethod<{id:string, to: WorkItemStatus, version: string}>(pipe(switchMap(p=> api.move(p.id, p.to, p.version).pipe(tapResponse({next: v=> patchState(store, updateEntity({id: v.id, changes: v})), error: e=> patchState(store,{error: e.detail})})))))
  })),
  withHooks({onInit(s){ s.load(); }})
);

// work-item-detail.store.ts — similar, con selectedEntity
export const WorkItemDetailStore = signalStore(
  withState<{item: WorkItemDetailAggregate|null}>({item: null}),
  withMethods((store, api=inject(WorkItemApi))=> ({
    load: rxMethod<string>(pipe(switchMap(id=> api.getDetail(id).pipe(tapResponse({next: v=> patchState(store,{item: v}), error: e=> patchState(store,{error:e})})))))
  }))
);
```

Mismo patrón para `projectsStore`, `myTasksStore`, `teamTasksStore`, `planningStore`, `documentsStore`, `aiQueueStore`, `notificationsStore` (badge `unreadCount` computed), `auditStore`, `adminStore`, `searchStore`, `orgStore`.

## Reglas

- `withState` → `withComputed` → `withMethods` (usa `rxMethod` + `switchMap` + `tapResponse` + `patchState`, nunca `mergeMap` que fuga) → `withProps` para deps → `withHooks.onInit` para carga → `withRequestStatus` → `withEntities` si lista.
- Nunca `HttpClient` en componente — solo en `ApiClient` inyectado vía `withProps`/`withMethods`.
- Nunca `BehaviorSubject` — lint/arch test lo prohíbe.

## Testing (skill patterns)

```ts
describe('KanbanStore', () => {
  it('load → entities', async () => {
    TestBed.configureTestingModule({ providers: [KanbanStore, {provide: KanbanApi, useValue: {list: ()=> of({items:[{id:'1'}], total:1})}}] });
    const store = TestBed.inject(KanbanStore);
    store.load();
    await vi.waitFor(()=> expect(store.entities().length).toBe(1));
  });
  it('switchMap cancela previo', ...);
});
```

Vitest + JSDOM, `ng test` con `include **/*.store.spec.ts`.

## Arch test

`no-behavior-subject-feature-state` ESLint + `NetArchTest` backend `NoCrossModuleDbContext`.
