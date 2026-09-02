# Tasks: API, UI and User Experience

**Input**: Design documents from `specs/009-api-ui-ux/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)
**Prerequisites**: plan.md (Tech Stack .NET 10 + Angular 22 + @ngrx/signals), spec.md (6 stories P1-P2), research.md (7 decisions), data-model.md (8 read models), contracts/ (5 contracts)
**Tests**: Included — TDD required per Constitution XX and Spec TDD Strategy (frontend store unit, E2E role/nav + Kanban, contract pagination/ProblemDetails/ETag)
**Organization**: Tasks grouped by user story for independent implementation and testing

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify Web/Api scaffolding and shared tooling before feature work

- [x] T001 Verify Web Angular 22 scaffolding and dependencies per plan in `src/Web/package.json` and `src/Web/src/app/app.routes.ts` (16 lazy routes placeholder)
- [x] T002 Verify Api vertical-slice wiring per plan in `src/Api/Program.cs` (`AddServiceDefaults`, `AddCqrs`, `AddEndpoints`, `AddOidcAuthentication`)
- [x] T003 [P] Verify design-skill references exist per plan in `.agents/skills/minimal-ui-design-system/references/tokens.md`, `components.md`, `layout.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shell, tokens, auth, API envelope and interceptors that ALL stories depend on — MUST complete before ANY US

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create design tokens SCSS mapping `references/tokens.md` to CSS vars in `src/Web/src/app/shared/tokens/tokens.scss` (bg `#F7F7F6`, card `#FFFFFF` shadow `0 8px 24px`, border `#ECECEC`, text `#111111/#777777`, radius `24px/14px/999px/18px`, Inter, grid `8px`, Tier 0/1/2/N)
- [x] T005 Create shell layout (sidebar 250px Tier 0 + top-bar Tier 1) per `references/components.md`/`layout.md` in `src/Web/src/app/core/layout/shell.component.ts` + `shell.component.html` + `shell.component.scss` (32px outer, 24px gap)
- [x] T006 [P] Create OIDC auth core (angular-auth-oidc-client) with AuthGuard/RoleGuard reading `sub`/`tenant_id`/`roles` in `src/Web/src/app/core/auth/auth.config.ts` and `src/Web/src/app/core/auth/role.guard.ts`
- [x] T007 [P] Create HTTP interceptors per `contracts/api-contracts.md` in `src/Web/src/app/core/interceptors/problem-details.interceptor.ts` (maps ProblemDetails to toast), `etag.interceptor.ts` (sends If-Match/version, handles 409/412), `tenant.interceptor.ts` (adds tenant header)
- [x] T008 [P] Create Api envelope and ProblemDetails client types per `contracts/api-contracts.md` in `src/Web/src/app/core/api/paged.model.ts` (`Paged<T> {items,total,page,pageSize}`) and `problem-details.model.ts`
- [x] T009 Create shared directive `*hasPermission`/`*hasBranch` per `contracts/navigation-and-access.md` in `src/Web/src/app/shared/pipes/has-permission.directive.ts`
- [x] T010 [P] Create withRequestStatus factory per `ngrx-signal-store` skill in `src/Web/src/app/shared/state/with-request-status.ts` (`signalStoreFeature`, `withState requestStatus`, `isPending/isFulfilled/error` computed)
- [x] T011 Verify Aspire orchestration hosts Api + Web per plan in `OroKanban.AppHost/AppHost.cs` (`AddProject("api")` + `AddNpmApp("web")` with `oroidentityserver` external)
- [x] T012 Create base ApiClient for envelope handling in `src/Web/src/app/core/api/api-client.service.ts` (typed get/post/put with Paged unwrap and ProblemDetails throw)

**Checkpoint**: Foundation ready — shell, tokens, guards, interceptors, stores factory verified; US implementation can now begin in parallel

---

## Phase 3: User Story 1 - Stable API Contracts with Pagination, Filtering, Sorting, Concurrency and ProblemDetails (Priority: P1) 🎯 MVP

**Goal**: Every list is `Paged<T>` + `Link`, every error is `ProblemDetails`, every mutation checks `ETag`/`version` → `409`/`412` (R1/XVI).

**Independent Test**: `GET /api/work-items?page=1&pageSize=10&sort=status` → envelope + Link; `PUT` stale `version=4` vs `5` → `409 ProblemDetails` with `currentVersion`; `GET ?filter=bad` → `400 ProblemDetails`. Contract tests without UI.

### Tests for User Story 1 ⚠️ Write FIRST, ensure FAIL

- [x] T013 [P] [US1] Contract test pagination envelope and Link header for work-items in `tests/Contract/ApiContractTests.cs` (page=2&pageSize=10 → total/page/Link)
- [x] T014 [P] [US1] Contract test concurrency 409/412 with stale version in `tests/Contract/ConcurrencyTests.cs` (PUT with If-Match → 409 + ProblemDetails.currentVersion)
- [x] T015 [P] [US1] Contract test ProblemDetails shape for validation 400 in `tests/Contract/ProblemDetailsTests.cs` (filter=bad → 400 title/detail/code)

### Implementation for User Story 1

- [x] T016 [US1] Implement `Paged<T>` envelope and `ResultExtensions.ToHttpResult` usage audit for all list queries in `src/Api/Features/WorkManagement/ListWorkItemsQuery.cs` (ensure `Specification<T>` + `total` + `Link`)
- [x] T017 [P] [US1] Add ETag/version handling to mutable handlers (WorkItem, Project, Document) in `src/Api/Features/WorkManagement/UpdateWorkItemCommand.cs` (check RowVersion → 409 ProblemDetails with currentVersion, GlobalExceptionHandler)
- [x] T018 [US1] Add ProblemDetails validation via `IValidator<T>` + `ValidationBehavior` for filter/sort params in `src/Api/Features/WorkManagement/ListWorkItemsValidator.cs` (unknown field → 400)
- [x] T019 [US1] Create Web ApiClient methods for paginated lists with filter/sort/search per `contracts/api-contracts.md` in `src/Web/src/app/core/api/work-items.api.ts` (`list(page,pageSize,filter,sort,q)` returns `Observable<Paged<WorkItem>>`)
- [x] T020 [US1] Update all list stores to delegate filter/sort/search to API (never client-side dump) in `src/Web/src/app/features/kanban/kanban.store.ts` (verify `rxMethod` passes params to `work-items.api.ts`)

**Checkpoint**: US1 fully functional — any list honors pagination/filter/sort, stale write surfaces 409 ProblemDetails without overwrite

---

## Phase 4: User Story 2 - Role and Branch-Aware Navigation with Security as API Authority (Priority: P1)

**Goal**: Nav/actions hide by `roles`+`IManagementHierarchy` subtree (UX), every command/query re-authorizes server-side (R2/R7, VII/XIX).

**Independent Test**: Login `Contributor` vs `Manager` (different branches) → DOM hides `Team Tasks`/`Administration`/`Audit` for Contributor, direct `GET /api/team-tasks` → `403`; Manager subtree sees only its projects.

### Tests for User Story 2

- [x] T021 [P] [US2] E2E role nav: Contributor hides management views in `src/Web/e2e/role-nav.spec.ts` (login Contributor → expect `team-tasks` link absent, `GET /api/team-tasks` → 403)
- [x] T022 [P] [US2] E2E branch isolation: Manager A vs B disjoint subtrees in `src/Web/e2e/branch-isolation.spec.ts` (same endpoint, different `tenant_id`/subtree → no cross rows)
- [x] T023 [P] [US2] Unit test RoleGuard + hasPermission directive in `src/Web/src/app/core/auth/role.guard.spec.ts` (roles vs requiredPermission, unauthorized hidden but API still 403)

### Implementation for User Story 2

- [x] T024 [US2] Implement `RoleGuard` reading `roles`/`tenant_id` from OIDC token and `requiredPermission`/`roles` in route `data` per `contracts/navigation-and-access.md` in `src/Web/src/app/core/auth/role.guard.ts`
- [x] T025 [US2] Implement `hasPermission` directive hiding DOM per permission/branch in `src/Web/src/app/shared/pipes/has-permission.directive.spec.ts` → `has-permission.directive.ts` (only hides, never authorizes)
- [x] T026 [US2] Configure 16 lazy routes with `canActivate: [AuthGuard, RoleGuard]` and `data` per `contracts/navigation-and-access.md` in `src/Web/src/app/app.routes.ts` (order: Dashboard→Organization→Projects→Kanban→WorkItemDetail→My/Team Tasks→Planning→Documents→Search→AI Queue→Notifications→Audit→Admin)
- [x] T027 [US2] Implement server-side re-authorization (every `IEndpoint` composes `tenant_id`+`IManagementHierarchy.GetSubtreeIds` before `Specification` fetch) audit in `src/Api/Features/Authorization/AuthorizationEvaluator.cs` (add check for team-tasks/audit endpoints → 403 ProblemDetails)
- [x] T028 [US2] Add deep-link test handling: `/admin` as Contributor → redirect to `/dashboard` in `src/Web/src/app/core/auth/role.guard.ts` (no flash)

**Checkpoint**: US1+US2 — nav is role/branch-aware, API remains sole authority

---

## Phase 5: User Story 3 - Manager Dashboard Subtree-Filtered (Priority: P1)

**Goal**: Dashboard shows `My Projects, My Team, My Sub-Managers, Overdue, Blocked, Critical, At Risk, Completed, AI Reviews pending, Document Reviews` all `IManagementHierarchy` subtree-filtered (R3, unbounded depth).

**Independent Test**: Two managers disjoint branches (3 projects each, 2 overdue only in A's subtree) → Manager A `Overdue=2`/`My Projects=3`, Manager B `Overdue=0`/`My Projects=3` via `GET /api/dashboard/kpis` + KPI cards Tier 2.

### Tests for User Story 3

- [x] T029 [P] [US3] Unit test dashboardStore computed `overdue`/`blocked` from `kpis` in `src/Web/src/app/features/dashboard/dashboard.store.spec.ts` (signalStore with withState/withComputed)
- [x] T030 [P] [US3] Integration test dashboard KPIs subtree-filtered (Manager A vs B) in `tests/Integration/DashboardSubtreeTests.cs` (seed 2 managers, call `GET /api/dashboard/kpis` with different `sub` → 0% leakage)
- [x] T031 [P] [US3] E2E dashboard KPI cards render and link to filtered queues in `src/Web/e2e/dashboard.spec.ts`

### Implementation for User Story 3

- [x] T032 [US3] Implement `GET /api/dashboard/kpis` read model (KPI enum + `IManagementHierarchy.GetSubtreeIds` before CountAsync) in `src/Api/Features/Dashboard/GetDashboardKpisQuery.cs` + `GetDashboardKpisHandler.cs` + `GetDashboardKpisEndpoint.cs`
- [x] T033 [US3] Create `DashboardStore` per skill (`signalStore/withState/withComputed/withMethods/withProps/withHooks/rxMethod/switchMap`) in `src/Web/src/app/features/dashboard/dashboard.store.ts` (`kpis, loading, error, filtered` computed, `load: rxMethod<void>`)
- [x] T034 [US3] Create Dashboard page (topBar Tier 1 + `kpi-card` Tier 2 grid, `chart-card` Tier 2, `list-card` recent, `avatar-row` flat) per `contracts/pages-spec.md` + `design-system.md` in `src/Web/src/app/features/dashboard/dashboard.page.ts` + `dashboard.page.html` + `dashboard.page.scss`
- [x] T035 [US3] Hide manager KPIs for Contributor (check `roles` in store computed) in `src/Web/src/app/features/dashboard/dashboard.page.ts` (conditional `*hasPermission`)

**Checkpoint**: US3 — dashboard KPIs are subtree-accurate, depth unlimited, cards Tier 2

---

## Phase 6: User Story 4 - Kanban and Work Item Detail with Progress Explanation (Priority: P2)

**Goal**: Kanban columns `Backlog→…→Completed` with drag-drop + state-machine enforcement, Detail shows all R4 sections with progress `Why?` link (R4 + XII + XIV).

**Independent Test**: Drag `In Progress→Blocked` → `PUT` ok + history; open Detail → `progress 66%` + `Why?` expands `subtasks 2/3, evidence` per SPEC-004; `aiInfo` hidden without `ai.review`; invalid `Completed→Backlog` → `409` + UI revert.

### Tests for User Story 4

- [x] T036 [P] [US4] Store test Kanban `move` rxMethod switchMap + tapResponse in `src/Web/src/app/features/kanban/kanban.store.spec.ts` (switchMap cancels previous, not mergeMap)
- [x] T037 [P] [US4] E2E Kanban round trip (drag, transition, 409 revert) in `src/Web/e2e/kanban.spec.ts` (drag card, verify `PUT` with version, invalid move → toast)
- [x] T038 [P] [US4] Unit test WorkItemDetail `progressExplanation` computed from `weighted subtasks` in `src/Web/src/app/features/work-item-detail/work-item-detail.store.spec.ts`

### Implementation for User Story 4

- [x] T039 [US4] Create `KanbanStore` with `withEntities<WorkItem>` + `filter.projectId` computed + `load`/`move` rxMethod per `contracts/state-stores.md` in `src/Web/src/app/features/kanban/kanban.store.ts`
- [x] T040 [US4] Create Kanban page (columns flat, cards Tier 2, drag-drop) per `references/components.md` in `src/Web/src/app/features/kanban/kanban.page.ts` + `kanban.page.html` + `kanban.page.scss` (uses `list-card` Tier 2, `badge` Tier 1)
- [x] T041 [US4] Enforce state-machine via API (`PUT /api/work-items/:id/status` with `IBusinessRule` `WorkItemStatusTransition`) and handle `409 ProblemDetails` revert in `src/Web/src/app/features/kanban/kanban.store.ts` (`move` error → `ProblemDetails` toast)
- [x] T042 [US4] Create `WorkItemDetailStore` (`item: WorkItemDetailAggregate|null`, `load: rxMethod<string>`) per `contracts/state-stores.md` in `src/Web/src/app/features/work-item-detail/work-item-detail.store.ts`
- [x] T043 [US4] Create Work Item Detail page with all R4 sections (header badge Tier 1, 2-col grid, progress `65%` + `progress-explanation` modal Tier 2, metrics badges Tier 1, subtasks/dependencies/documents/history/comments list-cards Tier 2, aiInfo gated) per `contracts/pages-spec.md` in `src/Web/src/app/features/work-item-detail/work-item-detail.page.ts` + `.html` + `.scss`
- [x] T044 [US4] Implement `GET /api/work-items/:id/detail` read model composing `WorkItemDetailAggregate` with `ProgressExplanation` per SPEC-004 in `src/Api/Features/WorkManagement/GetWorkItemDetailQuery.cs` + Handler + Endpoint (tenant+subtree filter)

**Checkpoint**: US4 — Kanban enforces transitions, Detail complete with progress explanation, AI gated

---

## Phase 7: User Story 5 - Minimum Views Shell (12 Views) with Consistent Design System (Priority: P2)

**Goal**: Shell provides 12 views reachable, every screen uses tokens/elevation from skill (R2/R5, XXII).

**Independent Test**: Render 12 routes → topBar flat, KPI cards elevated, lists flat, nav active pill Tier 2 + shadow `0 8px 24px` (visual regression/token audit).

### Tests for User Story 5

- [x] T045 [P] [US5] Visual regression/token audit for 12 views in `src/Web/e2e/design-system.spec.ts` (check `tokens.scss` vars, no hard-coded hex/shadow, nav active vs flat)
- [x] T046 [P] [US5] E2E shell navigation: all 12 views reachable + mobile collapsed nav in `src/Web/e2e/shell.spec.ts`

### Implementation for User Story 5

- [x] T047 [US5] Create reusable `shared/ui` controls with explicit Tier per `contracts/design-system.md` in `src/Web/src/app/shared/ui/`:
  - `kpi-card/kpi-card.component.ts` (Tier 2 `24px` + shadow, divider `#ECECEC`)
  - `list-card/list-card.component.ts` (Tier 2 card, rows flat, badge Tier 1)
  - `chart-card/chart-card.component.ts` (Tier 2, filter pill Tier 1, tooltip Tier 2)
  - `badge/badge.component.ts` (Tier 1 `999px` tint)
  - `button/button.component.ts` (primary black flat `999px`, secondary Tier 1)
  - `input/input.component.ts` (Tier 1 `18px` border)
  - `search-bar/search-bar.component.ts` (Tier 1 `18px`)
  - `filter-pill/filter-pill.component.ts` (Tier 1)
  - `pagination/pagination.component.ts` (envelope + Link)
  - `avatar-row/avatar-row.component.ts` (flat 52px)
  - `timeline/timeline.component.ts` (card Tier 2, items flat)
  - `modal/modal.component.ts` (Tier 2 `24px`)
- [x] T048 [US5] Implement remaining views reusing controls per `contracts/pages-spec.md` in:
  - `src/Web/src/app/features/projects/projects.page.ts` + `project-detail.page.ts`
  - `src/Web/src/app/features/my-tasks/my-tasks.page.ts`
  - `src/Web/src/app/features/team-tasks/team-tasks.page.ts`
  - `src/Web/src/app/features/planning/planning.page.ts`
  - `src/Web/src/app/features/documents/documents.page.ts` + `document-detail.page.ts`
  - `src/Web/src/app/features/search/search.page.ts`
  - `src/Web/src/app/features/ai-queue/ai-queue.page.ts`
  - `src/Web/src/app/features/notifications/notifications.page.ts`
  - `src/Web/src/app/features/audit/audit.page.ts`
  - `src/Web/src/app/features/admin/admin.page.ts`
  - `src/Web/src/app/features/organization/org-hierarchy.page.ts`
- [x] T049 [US5] Apply layout grid (`references/layout.md`: outer `32px`, gap `32px`, card padding `24-32px`, `8px` grid) to all pages in `src/Web/src/app/shared/tokens/layout.scss` and shell

**Checkpoint**: All 12+4 views reachable, tokens/elevation verifiable, no hard-coded styles

---

## Phase 8: User Story 6 - NgRx SignalStore State Management with Tests (Priority: P2)

**Goal**: All feature state lives in `SignalStore` per skill (`signalStore/withState/withComputed/withMethods/withProps/withEntities/withHooks/rxjs-interop`) with tests, no `BehaviorSubject`.

**Independent Test**: For any new feature store, verify `withState` + `withComputed` + `withMethods` (`rxMethod`/`switchMap`/`tapResponse`/`patchState`) + `withProps` + `withHooks(onInit)` + entity `withEntities` and tests follow `ngrx-signal-store/SKILL.md` patterns.

### Tests for User Story 6

- [x] T050 [P] [US6] Unit test `projectsStore` state/computed/methods per skill in `src/Web/src/app/features/projects/projects.store.spec.ts` (withState/withComputed/withMethods, rxMethod switchMap cancels)
- [x] T051 [P] [US6] Unit test `notificationsStore` unread badge computed in `src/Web/src/app/features/notifications/notifications.store.spec.ts`
- [x] T052 [P] [US6] Lint/arch test: no `BehaviorSubject` for feature state in `src/Web/e2e/arch.spec.ts` (ESLint `no-behavior-subject-feature-state` or custom)

### Implementation for User Story 6

- [x] T053 [P] [US6] Refactor `projectsStore` to `signalStore(withState,withEntities,withComputed,withMethods(withProps+rxMethod),withHooks)` per `contracts/state-stores.md` in `src/Web/src/app/features/projects/projects.store.ts`
- [x] T054 [P] [US6] Create `notificationsStore` (items, unreadCount computed, load/markRead rxMethod) per skill in `src/Web/src/app/features/notifications/notifications.store.ts`
- [x] T055 [P] [US6] Create remaining stores (`myTasksStore`, `teamTasksStore`, `planningStore`, `documentsStore`, `aiQueueStore`, `auditStore`, `adminStore`, `searchStore`, `orgStore`) per skill pattern in `src/Web/src/app/features/*/*.store.ts`
- [x] T056 [US6] Create `withRequestStatus` shared factory per skill in `src/Web/src/app/shared/state/with-request-status.ts` (already in T010, verify reuse across 12 stores)

**Checkpoint**: All 12 feature stores use `signalStore` + skill patterns, tests pass, no `BehaviorSubject`

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Tenant isolation, concurrency UX, performance and validation across all stories

- [x] T057 [P] Implement tenant isolation E2E: search + dashboard never return cross-tenant rows (0% leakage) in `tests/Integration/TenantIsolationTests.cs` and `src/Web/e2e/tenant-isolation.spec.ts` (Spec §SC-008)
- [x] T058 [P] Implement concurrency UX: 409/412 surface without data loss (preserve edits, offer Reload/Merge) in `src/Web/src/app/shared/ui/concurrency-modal/concurrency-modal.component.ts` (used by work-item-detail, kanban)
- [x] T059 [P] Handle pagination beyond total (page=999 → items=[] total unchanged) and `Link` absent per Spec edge case in `src/Web/src/app/core/api/api-client.service.ts` + `src/Api/Features/Shared/PaginationHelper.cs`
- [x] T060 [P] Add global ProblemDetails handler (toast) for all list views per Spec §FR-012 in `src/Web/src/app/core/interceptors/problem-details.interceptor.ts`
- [x] T061 Run design-system audit script (no hard-coded hex/spacing/shadow) per Spec §SC-006 in `src/Web/scripts/audit-tokens.mjs` (checks `tokens.scss` vars)
- [x] T062 Run quickstart validation per `specs/009-api-ui-ux/quickstart.md` (5 min manual: contracts, role nav, dashboard subtree, Kanban→detail, design, concurrency, tenant)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all US (shell, tokens, guards, envelope, interceptors)
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1 API Contracts) → US2 (P1 Nav) → US3 (P1 Dashboard) can be parallel after Foundational if staffed, but US1 first gives envelope for others
  - US4 (P2 Kanban/Detail) may start after Foundational, ideally after US1 (needs envelope/version)
  - US5 (P2 Views Shell) may start after Foundational, reuses tokens/controls from US4
  - US6 (P2 SignalStore) may start after Foundational, refactors stores from other USs — best after US3/US4 stores exist
- **Polish (Phase 9)**: Depends on all desired US being complete

### User Story Dependencies

- **US1 (P1) API Contracts**: Can start after Foundational — no dependencies on other stories; delivers envelope + ProblemDetails for all later
- **US2 (P1) Role/Branch Nav**: Depends on Foundational (RoleGuard, hasPermission) — may integrate with US1 (uses envelope) but independently testable via E2E
- **US3 (P1) Dashboard**: Depends on Foundational + US1 (Paged + subtree) — may integrate with US2 (role) but independently testable via API
- **US4 (P2) Kanban/Detail**: Depends on Foundational + US1 (ETag/version) — independent Kanban/detail flow
- **US5 (P2) Views Shell**: Depends on Foundational (tokens/elevation) — may integrate with US1-US4 but independently testable via visual regression
- **US6 (P2) SignalStore**: Depends on Foundational (withRequestStatus) — refactors stores, independent per feature

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation (TDD per Constitution XXI)
- Contracts/envelope before handlers
- Handlers before endpoints
- Stores withState → withComputed → withMethods (rxMethod/switchMap) → withProps → withHooks
- Controls Tier 1 before Tier 2
- Story complete before moving to next priority

### Parallel Opportunities

- All Phase 1 Setup tasks [P] can run in parallel
- All Phase 2 Foundational tasks T004, T006, T007, T008, T010 [P] can run in parallel (different files, no deps)
- Once Foundational completes, US1 (contract tests T013-T015) can run in parallel, US2 E2E T021-T023 can run in parallel, US3 T029-T031 can run in parallel
- All store unit tests per US marked [P] can run in parallel (different spec files)
- All `shared/ui` controls T047 sub-tasks can be built in parallel by different devs (different files)
- Different US can be worked in parallel by different team members after Foundational

---

## Parallel Example: User Story 1

```bash
# Launch all contract tests for US1 together (must fail before impl):
dotnet test tests/Contract/ApiContractTests.cs tests/Contract/ConcurrencyTests.cs tests/Contract/ProblemDetailsTests.cs --filter US1 &
pnpm --dir src/Web test -- --run --include="**/role.guard.spec.ts" &

# Launch all envelope/version handlers together:
# T016 ListWorkItems envelope + T017 ETag/version + T018 ValidationBehavior — different files, parallel if not same file
```

## Parallel Example: Foundational

```bash
# All tokens, guards, interceptors in parallel (no same-file conflicts):
# T004 tokens.scss + T006 auth.config/role.guard + T007 interceptors (3 files) + T008 paged.model + T010 with-request-status — 5 parallel agents
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T012) — CRITICAL blocks all stories
3. Complete Phase 3: US1 Stable API Contracts (T013-T020)
4. **STOP and VALIDATE**: Contract tests T013-T015 pass (envelope + 409 ProblemDetails + 400); manually `GET /api/work-items?page=1` + `PUT` stale → 409
5. Deploy/demo if ready — envelope unblocks all future UI

### Incremental Delivery

1. Setup + Foundational → Foundation ready (shell, tokens, guards, envelope)
2. Add US1 → Test independently → Deploy/Demo (MVP! — contracts stable)
3. Add US2 → Test independently → Deploy/Demo (nav role/branch-aware, API still 403)
4. Add US3 → Test independently → Deploy/Demo (dashboard subtree KPIs)
5. Add US4 → Test independently → Deploy/Demo (Kanban drag + detail progress Why?)
6. Add US5 → Test independently → Deploy/Demo (12 views shell, tokens Tier audited)
7. Add US6 → Test independently → Deploy/Demo (all 12 stores SignalStore, no BehaviorSubject)
8. Polish (Phase 9) → tenant isolation, concurrency modal, pagination edge, global handler, quickstart
9. Each story adds value without breaking previous

### Parallel Team Strategy

With multiple developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (API contracts) + US4 (Kanban/Detail) — envelope then Kanban
   - Developer B: US2 (Nav) + US3 (Dashboard) — guards then KPIs
   - Developer C: US5 (Views Shell + design system) + US6 (SignalStore) — controls then stores
3. Stories complete and integrate independently; Polish merges tenant/concurrency/validation

---

## Notes

- [P] tasks = different files, no dependencies — safe for parallel agents
- [Story] label maps task to specific user story for traceability (US1=Contracts, US2=Nav, US3=Dashboard, US4=Kanban/Detail, US5=Views, US6=SignalStore)
- Each user story independently completable and testable per its Independent Test in spec.md
- Verify tests fail before implementing (TDD per Constitution XXI)
- Commit after each task or logical group (e.g., after T004-T010 foundational)
- Stop at any checkpoint to validate story independently (quickstart 5 min manual)
- Avoid: vague tasks, same-file conflicts without coordination (app.routes.ts shared by US2/US5), cross-story dependencies that break independence (dashboard depends on US1 envelope — sequence accordingly)
- Constitution traceability: XVI (contracts), XIX (security API authority), XXII (minimal-ui-design-system + ngrx-signal-store), XV/VII (tenant/subtree), IV (Aspire)

---

## Phase 10: Convergence

- [x] T063 Add sidenav logout button wired to AuthService.logout() with OidcSecurityService per FR-005 and contracts/navigation-and-access.md (missing)
- [x] T064 Add theme toggle (change theme light/dark persisted to localStorage and CSS vars) to sidenav footer or topBar per FR-008 and design-system.md (missing)
- [x] T065 Implement notifications zone in topBar (bell icon with unreadCount badge and dropdown list-card) plus SignalR hub for real-time task notifications per FR-005/FR-014 and user feedback (missing)
- [x] T066 Wire Dashboard page to real data via DashboardStore and GET /api/dashboard/kpis subtree-filtered replacing hardcoded 24/128 per FR-006, SC-004, US3/AC1-3 and pages-spec.md (partial)
- [x] T067 Implement Projects list and project-detail pages with page-header, list-card Tier 2, filter-pill Tier 1, search-bar Tier 1, pagination and projectsStore integration per FR-004, FR-008 and pages-spec.md (partial)
- [x] T068 Implement My Tasks page with list-card where assignee==me, filter-pill, search-bar, pagination and myTasksStore per FR-004 and pages-spec.md (partial)
- [x] T069 Implement Planning page with chart-card milestones and list-card per FR-004 and pages-spec.md (partial)
- [x] T070 Implement Documents list and document-detail pages with list-card thumb, classification badge Tier 1, filter-pill, pagination and documentsStore per FR-004 and pages-spec.md (partial)
- [x] T071 Rework Search navigation concept from sidebar nav item to global topBar search-bar tenant-filtered while keeping Search results view per FR-011 and contracts/navigation-and-access.md (contradicts)
- [x] T072 Rework Notifications navigation concept from sidebar primary nav to topBar bell dropdown with list-card detail view per FR-004 and contracts/navigation-and-access.md (contradicts)
- [x] T073 Create missing SignalStores (myTasksStore, teamTasksStore, planningStore, documentsStore, aiQueueStore, auditStore, adminStore, searchStore, orgStore) following ngrx-signal-store skill with withState/withComputed/withMethods/rxMethod/switchMap per FR-009, SC-007 and contracts/state-stores.md (missing)
- [x] T074 Add consistent page-header title/subtitle pattern to all views (Projects, My Tasks, Planning, Documents, Search, Notifications, AI Queue, Audit, Admin, Organization) matching Dashboard per FR-008 and references/layout.md (partial)

