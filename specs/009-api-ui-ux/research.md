# Research: API, UI and User Experience

**Feature**: 009-api-ui-ux | **Date**: 2026-09-01 | **Status**: Complete

No `NEEDS CLARIFICATION` — todas las elecciones se resuelven con `constitution.md` + flujo `Foundation→Identity→Organization→Projects→Work→Metrics→Documents→Search→LLM→Audit→Notifications→Advanced` + skills `minimal-ui-design-system` y `ngrx-signal-store` + `BuildingBlocks`/`Api`/`Web` existentes.

## Decision 1: Páginas y flujo de navegación (16 rutas) siguiendo la constitution

- **Decision**: Navegación lineal por flujo de uso + jerarquía:
  1. `Login` (externo `oroidentityserver` OIDC, no página propia — redirect)
  2. `Dashboard` (`/dashboard`) — entrada tras login, subtree-filtered para Manager
  3. `Organization` (`/organization`) — árbol `OrganizationUnit`/`ManagementRelationship` ilimitado (de SPEC-002)
  4. `Projects` (`/projects`) + `Project Detail` (`/projects/:id`)
  5. `Kanban` (`/kanban?project=:id`) — columnas por `WorkItemStatus`
  6. `Work Item Detail` (`/work-items/:id`) — SPEC R4 completo
  7. `My Tasks` (`/my-tasks`, `assignee==sub`)
  8. `Team Tasks` (`/team-tasks`, subtree, Manager)
  9. `Planning` (`/planning`, milestones/metrics per SPEC-004)
  10. `Documents` (`/documents`) + `Document Detail` (`/documents/:id`, versiones, classification)
  11. `Search` (`/search?q=`, tenant-filtered)
  12. `AI Queue` (`/ai-queue`, `Generated→Pending Review`, permiso `ai.review`)
  13. `Notifications` (`/notifications`, badge `unread`)
  14. `Audit` (`/audit`, `audit.read`, trail append-only)
  15. `Administration` (`/admin`, org/roles, `Administrator`/`RootManager`)
  Además `Login` es externo. Las 12 mínimas de R2 están cubiertas; las 4 auxiliares (`Organization`, `Search`, `Project Detail`, `Document Detail`) completan el flujo constitution sin inventar vistas aisladas. `app.routes.ts` lazy-load por feature, `RoleGuard` solo oculta (API deniega).

- **Rationale**: Respeta `Initial Delivery Strategy` de la constitution y dependencias SPEC-002…008 — el usuario recorre `Identity → Organization → Projects → Work → Documents → AI → Audit → Notifications` sin saltos. Flujo verificable en `quickstart`.
- **Alternatives considered**: SPA plana con todas las vistas en nav de primer nivel sin orden (rechazada — pierde trazabilidad del flujo y sobrecarga nav); wizard obligatorio paso a paso (rechazado — enterprise necesita acceso directo por deep link).
- **ADR**: `ADR-009-01` flujo 16 rutas.

## Decision 2: API contracts first — envelope, ProblemDetails, ETag

- **Decision**: Todo endpoint usa DTOs estables en `src/Api/Features` + `IEndpoint`, nunca entidades (`BuildingBlocks.ServiceDefaults.Endpoints.ResultExtensions` `ToHttpResult/ToProblem`). Envelope `Paged<T> {items: T[], total:number, page:number, pageSize:number}` + header `Link` (`rel=next`). Errores siempre `ProblemDetails` (`type, title, detail, status, code` desde `Error`/`Result`). Concurrencia optimista vía `version` campo + header `ETag`/`If-Match` (o `RowVersion` base64) — `409 Conflict`/`412 Precondition Failed` con `ProblemDetails` que incluye `currentVersion`. Filtro/sort/search son query params (`?q=&filter=&sort=&page=&pageSize=`) delegados a `Specification<T>` en `Application`, no filtrado cliente. Validación tanto de campo como `IBusinessRule` retorna `400`.
- **Rationale**: Satisface R1/XVI y permite que Web y tests de contrato validen sin UI. Reusa `BuildingBlocks` canon ya cableado en `Api/Program.cs:92`.
- **Alternatives considered**: Exponer `AggregateRoot` directo (rechazado — fuga de invariantes); usar `400` plano sin `ProblemDetails` (rechazado — pierde trazabilidad); `Last-Modified` solo (rechazado — no basta para colisiones semánticas).
- **ADR**: `ADR-009-02` envelope + ProblemDetails + ETag.

## Decision 3: Design system — tokens y ELEVATION SYSTEM estricto

- **Decision**: Tokens verbatim de `references/tokens.md`: `Background #F7F7F6` (Tier 0), `Card #FFFFFF` `24px` `shadow 0 8px 24px rgba(0,0,0,.04)` (Tier 2), `Flat #FFFFFF/#FDFDFD` `border #ECECEC` sin shadow (Tier 1), `Text #111111/#777777/#A9A9A9`, `Green #63D471`, `Red #F26B6B`, `Button #111111` pill `999px`, `Nav pill 14px`, `Input 18px`, `Inter` Bold/Regular/Medium, grid `8px`. **Regla de elevación**: `Tier 0` fondo; `Tier 1` flat (search, filter pills, badges, icon buttons, inputs) — `background #FFFFFF` + opcional `border #ECECEC`, **sin shadow**; `Tier 2` elevated (KPI cards, list cards, chart cards, active nav pill, modals, floating buttons) — `background #FFFFFF` + `shadow 0 8px 24px`, hover `0 12px 32px` `200ms ease-in-out`; `Tier N` nav inactivo transparente `#777777` sobre fondo, hover `tint #F0F0EF` sin shadow. Implementado en `src/Web/src/app/shared/tokens/tokens.scss` importado globalmente.
- **Rationale**: R5/XXII mandatory — el skill dice que el error más común es dar sombra a todo lo blanco; solo Tier 2 flota. Así se preserva estética `Linear/Vercel` y se evita revisión fallida.
- **Alternatives considered**: Paleta custom por tenant (rechazado — out of scope, single instance); Tailwind default sin mapeo a Tier (rechazado — pierde flat vs elevated).
- **ADR**: `ADR-009-03` tokens/elevación.

## Decision 4: Componentes reutilizables (shared/ui) mapeados a skill

- **Decision**: 15 componentes en `src/Web/src/app/shared/ui/` cada uno con tier explícito:
  - `sidebar-nav` (columna 250px sobre Tier 0, items `Tier N` → hover tint → active `Tier 2` pill 14px + shadow)
  - `top-bar` (search Tier 1 pill 18px + primary CTA black pill + icon buttons Tier 1 + avatar)
  - `kpi-card` (Tier 2, 24px, stats con divider #ECECEC, delta badge Tier 1)
  - `list-card` (Tier 2 card + rows flat con divider, thumbnail/badge Tier 1)
  - `chart-card` (Tier 2 + filter pill Tier 1 + tooltip Tier 2 anidado)
  - `badge` (Tier 1 pill 999px, tint bg + solid text, 12px Medium)
  - `button` (primary black flat, secondary white Tier 1)
  - `input` (Tier 1, border #ECECEC, 18px)
  - `pagination` (envelope controls + Link header)
  - `filter-pill`/`search-bar` (Tier 1, no shadow)
  - `avatar-row` (flat 52px, no shadow)
  - `timeline` (audit trail, rows flat, card Tier 2)
  - `modal` (Tier 2, 24px + shadow)
  - `progress-explanation` (link que expande indicadores de SPEC-004)
  Cada componente consume tokens via CSS variables, nunca hex hardcodeado.
- **Rationale**: R5 + `references/components.md`/`layout.md` — reutilización garantiza consistencia en 12+ páginas y respeta `layout` grid `32px` outer / `24px` gap / `24-32px` card padding.
- **Alternatives considered**: Un componente por página (rechazado — divergencia); librería externa `Angular Material` sin skin (rechazado — rompe minimal aesthetic y Tier system).

## Decision 5: Estado con NgRx SignalStore per skill

- **Decision**: Un `signalStore` por feature en `src/Web/src/app/features/{dashboard,projects,kanban,work-item-detail,my-tasks,team-tasks,planning,documents,ai-queue,notifications,audit,admin,search,organization}/` con patrón skill:
  ```ts
  export const FeatureStore = signalStore(
    withState<{items: Entity[], loading: boolean, error: string|null, filter: Filter}>({items:[], loading:false, error:null, filter:{}}),
    withEntities<Entity>(),
    withComputed(({items, filter})=> ({ filtered: computed(()=> items().filter(...)) })),
    withMethods((store, api=inject(ApiClient))=> ({
      load: rxMethod<void>(pipe(switchMap(()=> api.get().pipe(tapResponse({next: v=> patchState(store, setAllEntities(v.items)), error: e=> patchState(store, setError(e))})))) ,
      update: rxMethod<Payload>(pipe(switchMap(p=> api.put(p, p.version).pipe(...))))
    })),
    withProps(()=> ({_api: inject(ApiClient)})),
    withHooks({ onInit(store){ store.load(); } }),
    withRequestStatus() // factory withRequestStatus del skill
  );
  ```
  `withProps` para deps inyectadas, `withHooks.onInit` carga, `rxMethod` + `switchMap` + `tapResponse` + `patchState`, nunca `BehaviorSubject`. Tests con `TestBed` + `patchState`/`getState` por skill. Lint prohíbe `BehaviorSubject` en features.
- **Rationale**: R6/XXII mandatory — `ngrx-signal-store/SKILL.md` exige `signalStore/withState/withComputed/withMethods/withProps/withEntities/withHooks/rxjs-interop`. Asegura cancelación de previos y testeabilidad.
- **Alternatives considered**: `NgRx Store` clásico con actions/reducers (rechazado — más verboso, no requerido por skill); servicios con `BehaviorSubject` (rechazado — prohibido por skill).

## Decision 6: Seguridad — UI oculta, API deniega (dual)

- **Decision**: `RoleGuard` + directiva `*hasPermission`/`*hasBranch` en `nav`/`top-bar`/`work-item-detail` ocultan botones/vistas según `roles`/`tenant_id` + `IManagementHierarchy` subtree; pero cada `IEndpoint` re-valida `IAuthorizationEvaluator` + `Specification` con `tenant_id`+subtree antes del fetch. `Contributor` no ve `Team Tasks` pero `GET /api/team-tasks` → `403 ProblemDetails` independiente. Deep link `/admin` como `Contributor` redirige y la API deniega.
- **Rationale**: R7/XIX — hiding es UX, no seguridad. Defense-in-depth.
- **Alternatives considered**: Solo ocultar en UI (rechazado — bypass por API directo); solo denegar en API sin ocultar (rechazado — mala UX).

## Decision 7: Tecnologías y tooling ya existentes

- **Decision**: Reusar `Angular 22.1` + `Typescript 6` + `Vite` + `Vitest`/`JSDOM` ya en `src/Web/package.json`, `Aspire` ya en `AppHost`. No introducir `MediatR`/`MassTransit`/`AutoMapper` (Principio I). Añadir solo `@ngrx/signals` (ya `@ngrx/signals` 22 presente) + `@ngrx/signals/entities` + `@ngrx/signals/rxjs-interop`.
- **Rationale**: Principio I/III/XXI — ya existe stack, no reinventar orquestación ni CQRS.
