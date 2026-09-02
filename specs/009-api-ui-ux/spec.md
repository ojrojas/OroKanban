# Feature Specification: API, UI and User Experience

**Feature Branch**: `009-api-ui-ux`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "SPEC-009 — API, UI and User Experience — Bounded Contexts: BC-10 Platform + all read models · Depends on: SPEC-002…SPEC-008 — Mandatory skills: minimal-ui-design-system, ngrx-signal-store — Objective: Provide an intuitive enterprise UI where security is enforced by the API, role/branch-aware navigation, and a consistent design system. Requirements R1–R7, views, dashboard, work item detail, design system, state management, security posture, acceptance criteria, TDD strategy."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stable API Contracts with Pagination, Filtering, Sorting, Concurrency and ProblemDetails (Priority: P1)

As any consumer (UI or integration) I interact with APIs through stable contracts that never leak internal entities; every list supports pagination envelope, filtering, sorting, search, and every mutation is guarded by optimistic concurrency; all errors are consistent `ProblemDetails`.

**Why this priority**: R1 is the foundation for every view. Without contract stability, UI cannot be built reliably and concurrency without ETag/version causes silent overwrites. This realizes Constitution XVI.

**Independent Test**: Issue `GET /api/work-items?page=1&pageSize=10&sort=status&filter=overdue` and verify pagination envelope `{items,total,page,pageSize}` + `Link` header; then `PUT` with stale `If-Match`/`version` and verify `409`/`412` with `ProblemDetails`; finally request with invalid filter and verify `400` `ProblemDetails` with validation errors. Can be tested solely via contract tests without any UI.

**Acceptance Scenarios**:

1. **Given** 25 work items exist, **When** client requests `GET /api/work-items?page=2&pageSize=10`, **Then** response contains 10 items, `total=25`, `page=2`, and `ProblemDetails` is not used (200 with envelope).
2. **Given** a work item with version `5`, **When** client submits update with `version=4` (stale), **Then** API returns `409 Conflict` or `412 Precondition Failed` with `ProblemDetails` (`title`, `detail`, `status`, `code`), and no data is overwritten.
3. **Given** a list query with `filter=unknownField`, **When** executed, **Then** API returns `400` with `ProblemDetails` describing validation error.
4. **Given** any error (auth, validation, conflict, not found), **When** returned, **Then** body is `ProblemDetails` via `Result → HTTP`, not raw entity or stack trace.

---

### User Story 2 - Role and Branch-Aware Navigation with Security as API Authority (Priority: P1)

As a user with a specific role and organizational branch, I see only the navigation and actions my role/branch permits, but I understand hiding is UX only — the API independently denies unauthorized requests.

**Why this priority**: R2 (minimum views) + R7 (UI hides, API enforces) + Constitution VII/XIX. Without branch-aware navigation, users see forbidden items; without API authority, hiding alone is insecure.

**Independent Test**: Log in as `Contributor` vs `Manager` (different branches). As Contributor, verify management views (`Team Tasks`, `Administration`, `Audit`, `My Sub-Managers`) are absent in DOM and direct API call `GET /api/team-tasks` returns `403 ProblemDetails`. As Manager, verify same API returns 200 with subtree data. Can be tested via E2E navigation tests without dashboard data.

**Acceptance Scenarios**:

1. **Given** a Contributor is authenticated, **When** the app renders, **Then** navigation shows `Dashboard`, `My Tasks`, `Projects`, `Documents` but hides `Team Tasks`, `Administration`, and `Audit`; direct API call to `GET /api/audit/entries` returns `403`.
2. **Given** a Manager in branch `A` (manages 5 projects) and a Manager in branch `B`, **When** each opens the same endpoint, **Then** each sees only their subtree's projects (branch filtering verified by API, not just UI filter).
3. **Given** an unauthorized UI action is hidden (e.g., `Approve Document` button), **When** the API is called directly with that command, **Then** it returns `403 ProblemDetails` regardless of UI state.
4. **Given** a user switches role via token change, **When** navigation re-renders, **Then** visible items update without full page reload.

---

### User Story 3 - Manager Dashboard Subtree-Filtered (Priority: P1)

As a Manager I open the dashboard and see `My Projects`, `My Team`, `My Sub-Managers`, `Overdue`, `Blocked`, `Critical`, `At Risk`, `Completed`, `AI Reviews pending`, `Document Reviews` — all counts are scoped to my organizational subtree.

**Why this priority**: R3 is the primary value for management. Without subtree filtering, dashboards leak cross-branch data and violate XV.

**Independent Test**: Create two managers each with 3 projects in separate subtrees. Assign 2 overdue items to Manager A's subtree only. Log in as Manager A and verify dashboard cards show `Overdue=2`, `My Projects=3`; log in as Manager B and verify `Overdue=0`, `My Projects=3` (their own). Can be tested via API `GET /api/dashboard` + UI KPI cards.

**Acceptance Scenarios**:

1. **Given** Manager `M1` manages org unit `OU-A` (5 work items: 2 overdue, 1 blocked, 1 critical), **When** `M1` opens dashboard, **Then** KPI cards reflect `Overdue=2`, `Blocked=1`, `Critical=1` for `OU-A` subtree only.
2. **Given** `M1` has 2 direct reports and 1 sub-manager who manages 3 others, **When** dashboard loads, **Then** `My Team=5`, `My Sub-Managers=1` (subtree expansion, depth not hard-coded).
3. **Given** there are `AI Reviews pending=4` and `Document Reviews=2` in `M1`'s subtree, **When** dashboard loads, **Then** those cards appear with counts and link to review queues filtered to subtree.
4. **Given** Contributor opens dashboard, **When** rendered, **Then** manager KPI cards are absent.

---

### User Story 4 - Kanban and Work Item Detail with Progress Explanation (Priority: P2)

As a team member I use Kanban to move work through `Backlog → Planned → In Progress → Blocked → In Review → Completed` and open Work Item Detail to see description, responsible, manager, status, progress (with explanation link), metrics, subtasks, dependencies, documents, history, comments, and authorized AI information.

**Why this priority**: R4 is the core work surface. Without detail completeness, users cannot execute work; without progress explanation, Constitution XII is violated.

**Independent Test**: Drag a card from `In Progress` to `Blocked` on Kanban and verify status transition via API and history entry; then open Work Item Detail and verify all sections render, `progress` shows `65%` plus `Why?` link that expands explanation (weighted subtasks, evidence, metrics), and AI section is hidden if user lacks `ai.review` permission.

**Acceptance Scenarios**:

1. **Given** a work item in `In Progress` with 3 subtasks (2 done), **When** detail opens, **Then** it shows `description`, `responsible`, `manager`, `status=In Progress`, `progress=66%` with `explanation` link, `metrics`, `subtasks`, `dependencies`, `documents`, `history`, `comments`.
2. **Given** user clicks progress explanation, **When** expanded, **Then** it lists inputs: `completed subtasks 2/3`, `approved evidence`, `metric X` per Spec 004, not an arbitrary number.
3. **Given** user without `ai.review` tries to view AI information, **When** detail loads, **Then** AI section is hidden and direct `GET /api/work-items/{id}/ai` returns `403`.
4. **Given** user moves card on Kanban to an invalid transition (`Completed → Backlog`), **When** dropped, **Then** API returns `409` with `ProblemDetails` and UI reverts the move with error toast.

---

### User Story 5 - Minimum Views Shell (12 Views) with Consistent Design System (Priority: P2)

As any user I navigate a shell providing the 12 minimum views: `Dashboard`, `Projects`, `Kanban`, `Work Item Detail`, `My Tasks`, `Team Tasks`, `Planning`, `Documents`, `AI Processing (review queue)`, `Notifications`, `Audit`, `Administration` — every screen uses `minimal-ui-design-system` tokens (colors, typography, spacing, radius) and ELEVATION SYSTEM (flat vs shadow-elevated) and component patterns (nav, top bar, KPI cards, lists, badges).

**Why this priority**: R2 defines scope completeness; R5 is mandatory skill (Principle XXII). Without design system, UI diverges and review fails.

**Independent Test**: Render each of the 12 routes and verify via visual regression / design token audit: top bar is `flat` (no shadow), KPI cards are `elevated` (`shadow-elevated`), lists use `flat` with token `spacing-4`, `radius-md`, typography `font-sans`, colors from palette — all via skill `references/` files. Can be tested via Storybook + design review checklist without business logic.

**Acceptance Scenarios**:

1. **Given** any new screen is implemented, **When** reviewed, **Then** `colors`, `typography`, `spacing`, `radius` values match `minimal-ui-design-system` tokens and `elevation` is correctly chosen per surface type (verifiable in review).
2. **Given** user navigates to each of the 12 views, **When** loaded, **Then** nav highlights active item, top bar persists, and layout uses skill's `references/layout` grid.
3. **Given** navigation on mobile width, **When** rendered, **Then** required views remain reachable via collapsed nav (mobile not out of scope for shell).
4. **Given** design system skill updates tokens, **When** tokens change, **Then** all views reflect new tokens without per-screen patching.

---

### User Story 6 - NgRx SignalStore State Management with Tests (Priority: P2)

As a frontend developer I manage all UI state via NgRx SignalStore per `ngrx-signal-store` skill: `signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, entity features, lifecycle hooks, `rxjs-interop` for API calls — and tests follow skill patterns.

**Why this priority**: R6 is mandatory skill (Principle XXII). Without SignalStore, state diverges per feature and becomes untestable.

**Independent Test**: For a feature store (e.g., `workItemsStore`), verify `withState({items, loading, error})`, `withComputed(selectors)`, `withMethods` calling API via `rxMethod`/`switchMap`, `withProps` for derived, `withHooks(onInit)` loading, and unit tests using skill's `provideMockStore`/`spectator` patterns — no ad-hoc `BehaviorSubject` services.

**Acceptance Scenarios**:

1. **Given** a new feature screen, **When** implemented, **Then** state lives in a `signalStore` with `withState` + `withComputed` + `withMethods` + `withProps` + entity `withEntities` (if list) and no direct `HttpClient` in component.
2. **Given** store method `loadWorkItems`, **When** called, **Then** it uses `rxMethod` + `switchMap` to API, sets `loading`, handles `error` via `ProblemDetails`, and updates entities.
3. **Given** store tests run, **When** executed, **Then** they follow `ngrx-signal-store` testing patterns (state, computed, methods) and pass.
4. **Given** concurrent loads, **When** triggered, **Then** `switchMap` cancels previous, not `mergeMap` leak.

---

### Edge Cases

- Pagination beyond total: `page=999` returns `items=[]` with `total` unchanged and `Link` absent, not error.
- Stale version race: two users edit same work item (version 5) simultaneously; second submit gets `409` with `ProblemDetails` containing `currentVersion`, UI offers `Reload` and merges without data loss.
- Subtree with unbounded depth: manager managing managers 5 levels deep still returns correct dashboard counts (no hard-coded depth).
- Design token missing: component falls back to skill's default token, not hard-coded hex.
- Store error: API returns `ProblemDetails` 500, SignalStore sets `error` signal, UI shows retry toast, not blank screen.
- Unauthorized deep link: user pastes `/admin` as Contributor → UI redirects to dashboard and API returns `403`; no flash of admin content.
- Search with tenant isolation: search query never returns cross-tenant results even if UI filter omitted (API enforces XV).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST expose stable API contracts for every resource (per Constitution XVI): request/response DTOs separate from internal entities, pagination envelope `{items, total, page, pageSize, Link}`, filtering, sorting, search, and consistent `ProblemDetails` error shape via `Result → HTTP`.
- **FR-002**: System MUST support optimistic concurrency for all mutable resources (work items, projects, documents, plan configs) via `ETag`/`If-Match` or `version` field; stale writes MUST return `409`/`412` with `ProblemDetails` containing current version, never silent overwrite.
- **FR-003**: System MUST validate every request (field, business rule) and return `400` with `ProblemDetails` containing validation errors; validation lives in application layer (`IBusinessRule`/`Specification`), not only in UI.
- **FR-004**: UI MUST provide the minimum 12 views: `Dashboard`, `Projects`, `Kanban`, `Work Item Detail`, `My Tasks`, `Team Tasks`, `Planning`, `Documents`, `AI Processing (review queue)`, `Notifications`, `Audit`, `Administration` — each reachable via navigation and directly via deep link.
- **FR-005**: Navigation and action visibility MUST be role and branch-aware (Contributor, Manager, Administrator, Auditor, etc.) and driven by `oroidentityserver` claims + `IManagementHierarchy` subtree; hiding is UX only — every command/query MUST be authorized server-side.
- **FR-006**: Manager dashboard MUST display `My Projects`, `My Team`, `My Sub-Managers`, `Overdue`, `Blocked`, `Critical`, `At Risk`, `Completed`, `AI Reviews pending`, `Document Reviews` — all computed via subtree-filtered queries (unbounded depth, no hard-coded level).
- **FR-007**: Work Item Detail MUST display `description`, `responsible`, `manager`, `status`, `progress` with explanation link (per Spec 004, revealing weighted indicators), `metrics`, `subtasks`, `dependencies`, `documents`, `history`, `comments`, and `authorized AI information` (hidden if `ai.review` denied).
- **FR-008**: All UI surfaces MUST follow `minimal-ui-design-system` skill: tokens `colors`, `typography`, `spacing`, `radius` and **ELEVATION SYSTEM** (`flat` for nav/top bar/lists, `shadow-elevated` for KPI cards/modals) and component patterns `nav`, `top bar`, `KPI cards`, `lists`, `widgets`, `buttons`, `badges` from `references/` files. New screens MUST be verified against skill before merge.
- **FR-009**: Frontend state MUST use NgRx SignalStore per `ngrx-signal-store` skill: `signalStore`, `withState`, `withComputed`, `withMethods`, `withProps`, entity features (`withEntities`), lifecycle hooks (`withHooks`), `rxjs-interop` (`rxMethod`, `switchMap`) for API calls. Direct `BehaviorSubject` services for feature state are prohibited.
- **FR-010**: UI MUST surface API concurrency errors (`409`/`412`) without data loss: show `ProblemDetails` detail, offer `Reload`/`Merge` action, preserve user edits in form.
- **FR-011**: Search, filter, sort MUST be delegated to API (not client-side filtering of full dump); UI sends `q`, `filter`, `sort`, `page`, `pageSize` and renders envelope.
- **FR-012**: Every list view MUST honor pagination/filter/sort/sort contracts and display `ProblemDetails` errors via global handler, not silent failure.
- **FR-013**: Kanban MUST enforce state-machine transitions (Spec 014) via API: invalid moves return `409` and UI reverts.
- **FR-014**: All API responses MUST be tenant/organization-aware (XV): queries include `tenant_id` and subtree predicate before fetch; UI never bypasses.
- **FR-015**: System MUST provide consistent auth: `oroidentityserver` via `access_token` (Authorization header), no direct DB access to IdP (Principle II).

### Key Entities *(include if feature involves data)*

- **ApiContract**: Stable DTOs per resource (`ProjectResponse`, `WorkItemResponse`, `DocumentResponse`, pagination envelope `Paged<T>`, `ProblemDetails`); never exposes internal `Entity`/`AggregateRoot`.
- **View**: One of 12 minimum views (Dashboard, Projects, Kanban, Work Item Detail, My Tasks, Team Tasks, Planning, Documents, AI Review Queue, Notifications, Audit, Administration) — each has route, navigation entry, and required permission.
- **DashboardKPI**: Manager metric card (`Overdue`, `Blocked`, `Critical`, `At Risk`, `Completed`, `My Projects`, `My Team`, `My Sub-Managers`, `AI Reviews pending`, `Document Reviews`) — value computed subtree-filtered.
- **WorkItemDetailAggregate**: Read model composing work item + responsible/manager + status + progress (with `ProgressExplanation`) + metrics + subtasks + dependencies + documents + history + comments + AI info (permission-gated).
- **DesignToken**: `colors`, `typography`, `spacing`, `radius`, `elevation` (flat vs shadow-elevated) from `minimal-ui-design-system` skill's `references/`.
- **SignalStore**: NgRx `signalStore` instance per feature (`workItemsStore`, `projectsStore`, `dashboardStore`) with `withState`, `withComputed`, `withMethods`, `withProps`, `withEntities`, `withHooks`, `rxMethod`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All list APIs honor pagination envelope and return `ProblemDetails` for errors — contract tests pass 100% for pagination, filter, sort, and error shape across work items, projects, documents, audit.
- **SC-002**: Stale concurrent edits surface `409`/`412` with `ProblemDetails` and UI preserves edits — 95% of concurrency conflicts result in visible retry without data loss in E2E.
- **SC-003**: Contributor sees zero management views and direct API calls to those resources are denied (`403`) — role-based E2E passes for 4 roles × 12 views.
- **SC-004**: Manager dashboard counts are subtree-accurate — for two managers in disjoint branches with known data, dashboard KPIs differ exactly as seeded (0% cross-branch leakage).
- **SC-005**: Any list view renders paginated results and correctly handles `Link` header — manual verification for all 12 views shows correct `page`/`total` and `ProblemDetails` toast on failure.
- **SC-006**: New screens pass design-system review: 100% of components use tokens/elevation from skill's `references/` (no hard-coded hex/spacing/shadow), verifiable via token audit.
- **SC-007**: All feature state lives in SignalStores with `signalStore` + `withState`/`withComputed`/`withMethods`/`withProps` and tests follow skill patterns — lint rule/arch test enforces no `BehaviorSubject` feature state, store unit tests pass.
- **SC-008**: Tenant isolation holds in UI-driven flows — search and dashboard never return cross-tenant rows (0% leakage in integration tests).

## Assumptions

- `oroidentityserver` is external Podman container (Principle II) — auth via `access_token`/`tenant_id` claim, no direct DB access to IdP.
- `.NET 10` + `BuildingBlocks` (`IEndpoint`, `Result→HTTP`, `GlobalExceptionHandler`) + `Aspire` orchestration (Principle III/IV) are already wired; this spec adds contracts/views on top.
- Existing modules `Identity`, `Organization`, `Projects`, `Documents`, `AiProcessing`, `Audit`, `Notifications` already expose filtered queries; dashboard reuses them with `IManagementHierarchy` subtree predicate.
- `WorkItem` state machine `Backlog→Planned→In Progress→Blocked→In Review→Completed` and `ProgressExplanation` logic from Spec 004 are reused verbatim.
- Design tokens are those defined in `.agents/skills/minimal-ui-design-system/references/` — no custom palette introduced.
- SignalStore skill version is that in `.agents/skills/ngrx-signal-store` — `withEntities` for lists, `rxMethod` for API interop.
- Mobile shell is collapsed nav, not separate native app — 12 views remain reachable.
- Error handling uses `ProblemDetails` (`title`, `detail`, `status`, `code`) per `BuildingBlocks.ServiceDefaults` `ResultExtensions`.

## Dependencies

- SPEC-002 Identity & Organization (hierarchy, `IManagementHierarchy`, roles).
- SPEC-003 Projects & Kanban (WorkItem, status transitions).
- SPEC-004 Metrics/Progress (progress explanation link).
- SPEC-005 Documents (document list/detail, classification).
- SPEC-006 LLM (AI review queue, `ai.review` permission).
- SPEC-007 Audit (Audit view, `audit.read`).
- SPEC-008 Notifications (Notifications view, unread badge).
- Constitution XVI (API contracts), XIX (security), XXII (skills `minimal-ui-design-system`, `ngrx-signal-store`).

## Out of Scope

- Native mobile apps (iOS/Android) — web shell with responsive collapsed nav suffices.
- Real-time collaborative cursors / OT — Kanban uses optimistic concurrency, not live presence.
- Custom theming per tenant — single design system instance.
- Public unauthenticated API — all endpoints require `access_token`.

## Constitution Traceability

- XVI APIs Are Contracts — stable DTOs, pagination, version, ProblemDetails.
- XIX Security by Default — least privilege, API authoritative, no IdP DB access, deny-by-default.
- XXII Workspace Skills — `minimal-ui-design-system` (tokens, elevation, components) and `ngrx-signal-store` (signalStore, withState/computed/methods) as mandatory rule bases.
- XV Tenant/Organization Aware — subtree filtering for dashboard/search.
- VII Hierarchical Authorization — unbounded depth for dashboard.
- IV Aspire Is Orchestrator — AppHost hosts Api + Web.

