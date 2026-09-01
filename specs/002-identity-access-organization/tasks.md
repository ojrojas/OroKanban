# Tasks: Identity, Access and Organization

**Input**: Design documents from `/specs/003-identity-access-organization/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/hierarchy-contract.md, contracts/authorization-contract.md, contracts/permission-catalog-contract.md, quickstart.md
**Constitution**: v1.2.0 enforced — Principles II, VI, VII, VIII, XV, XIX, XX, XXI
**Depends on**: 002-foundation-architecture (AppDbContextBase, AppHost with postgres/redis, Api JWT wiring)

**Tests**: Unit (cycle rules, grant expiry, evaluator composition, IsSatisfiedBy), Integration (hierarchy CTE, Redis invalidation, OIDC claim mapping), Security matrix (8 actor types) — all REQUIRED per spec TDD Strategy.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Tooling verification and ADR recording for hierarchy storage decision

- [x] T001 Verify tooling versions match `global.json` 10.0.400 and `oroidentityserver` discovery endpoint is reachable at configured `Identity__Authority` in `src/Api/appsettings.Development.json`
- [x] T002 Record hierarchy storage ADR at `docs/adr/adr-004-hierarchy-storage.md` (recursive CTE on adjacency list per research Decision 1 — WithRecursive indexes, not closure table/ltree) referencing `draft/discovery/000-repository-catalog.md`
- [x] T003 Ensure `src/Modules/Organization` and `src/Modules/Identity` projects already scaffolded by 002 are buildable (`dotnet build src/Modules/Organization/Organization.Domain/Organization.Domain.csproj` succeeds)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared domain primitives and contracts that MUST be complete before ANY hierarchy or authorization work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 Create `Organization.Contracts/IManagementHierarchy` Shared Kernel interface in `src/Modules/Organization/Organization.Contracts/IManagementHierarchy.cs` per `contracts/hierarchy-contract.md` (IsInSubtree, GetSubtree, GetAncestors, GetCommonAncestor, tenant-scoped)
- [x] T005 [P] Create `Identity.Domain/ValueObjects/PermissionCode` value object in `src/Modules/Identity/Identity.Domain/ValueObjects/PermissionCode.cs` per `contracts/permission-catalog-contract.md`
- [x] T006 [P] Create hierarchy value objects `HierarchyPath`, `SubtreeScope`, `GrantScope` in `src/Modules/Organization/Organization.Domain/ValueObjects/HierarchyPath.cs` etc. per `data-model.md`
- [x] T007 Create `Organization.Domain/Errors/OrganizationErrors` static error definitions (CycleDetected, SelfReference, DuplicateActiveRelationship, GrantExpired) in `src/Modules/Organization/Organization.Domain/Errors/OrganizationErrors.cs` using `Error.Validation/Conflict`
- [x] T008 Scaffold `OrganizationDbContext` schema `organization` with `OutboxEntityTypeConfiguration` in `src/Modules/Organization/Organization.Infrastructure/Persistence/OrganizationDbContext.cs` (extends `AppDbContextBase`, `HasDefaultSchema("organization")`) if not already present from 002
- [x] T009 Scaffold `IdentityDbContext` (or reuse `OrganizationDbContext`) for permission catalog seeding in `src/Modules/Identity/Identity.Infrastructure/Persistence/IdentityDbContext.cs` (extends `AppDbContextBase`, schema `identity`) if not already present
- [x] T010 Define consumed interface stub `Organization.Domain/Services/IProjectMembership` (`IsMember(userId, resourceId)`) in `src/Modules/Organization/Organization.Domain/Services/IProjectMembership.cs` (stub returns false until Projects spec)

**Checkpoint**: Foundational contracts and contexts ready — hierarchy aggregates and evaluator can now be built

---

## Phase 3: User Story 1 - Manager establishes and maintains reporting hierarchy (Priority: P1) 🎯 MVP

**Goal**: Cycle-free, arbitrary-depth `ManagementRelationship` + `OrganizationUnit` aggregates with domain events and cache-aware subtree queries

**Independent Test**: Seed users A, B, C; `AssignManager(A→B)` succeeds and `GetAncestors(C)` after `A→B→C` contains `B, A`; then `AssignManager(C→A)` is rejected with `Error.Validation` and `IsInSubtree(A, C)` remains true while `IsInSubtree(C, A)` remains false; after a `ManagerAssignedToUser` event, a cached `GetSubtree(A)` repopulates with the new member

### Tests for User Story 1 (write FIRST, ensure FAIL before implementation)

- [x] T011 [P] [US1] Unit test `SubtreeCannotContainManagerRule` cycle detection in `tests/Organization.Tests/Domain/SubtreeCannotContainManagerRuleTests.cs` (A→B→C, then C→A rejected; self-reference A→A rejected)
- [x] T012 [P] [US1] Unit test single-active-per-unit invariant in `tests/Organization.Tests/Domain/ManagementRelationshipTests.cs` (duplicate active AssignManager for same subordinate+unit → Conflict)
- [x] T013 [P] [US1] Unit test `ExplicitGrant.IsSatisfiedBy` expiry in `tests/Organization.Tests/Domain/ExplicitGrantTests.cs` (future expiresAt → true, past → false, null → true)

### Implementation for User Story 1

- [x] T014 [P] [US1] Create `ManagementRelationshipId`, `OrganizationUnitId`, `ExplicitGrantId` StronglyTypedId records in `src/Modules/Organization/Organization.Domain/ValueObjects/Ids.cs`
- [x] T015 [US1] Implement `ManagementRelationship` aggregate root (`ManagementRelationshipId`, `TenantId`, `ManagerId`, `SubordinateId`, `Type`, `OrganizationUnitId`, `ValidFrom/To`, `RowVersion`) with `CheckRule(ManagerCannotBeSubordinateRule)` + `SubtreeCannotContainManagerRule`, and events `ManagerAssignedToUser`/`ManagerRemovedFromUser` in `src/Modules/Organization/Organization.Domain/Aggregates/ManagementRelationship.cs`
- [x] T016 [US1] Implement `OrganizationUnit` aggregate (tree, `HierarchyPath` VO) with events `OrganizationUnitCreated`/`OrganizationUnitMoved` in `src/Modules/Organization/Organization.Domain/Aggregates/OrganizationUnit.cs`
- [x] T017 [US1] Implement `ExplicitGrant` aggregate with `IsExpired`/`IsSatisfiedBy` and events `GrantIssued`/`GrantRevoked` in `src/Modules/Organization/Organization.Domain/Aggregates/ExplicitGrant.cs`
- [x] T018 [US1] Implement EF configurations for `ManagementRelationship` and `OrganizationUnit` (table `organization.management_relationships` with indexes `(tenant_id, manager_id)` and `(tenant_id, subordinate_id)`, filtered unique index for single active per subordinate/unit) in `src/Modules/Organization/Organization.Infrastructure/Persistence/Configurations/ManagementRelationshipConfiguration.cs`
- [x] T019 [US1] Implement `OrganizationDbContext` DbSets + `ApplyConfiguration` for new entities in `src/Modules/Organization/Organization.Infrastructure/Persistence/OrganizationDbContext.cs` (register configurations, ensure `HasDefaultSchema("organization")`)
- [x] T020 [US1] Implement `ManagementHierarchyService` (`IManagementHierarchy`) with recursive CTE queries for `IsInSubtree`/`GetSubtree`/`GetAncestors`/`GetCommonAncestor` + Redis cache (`hierarchy:{tenant}:{managerId}:subtree`) with TTL 5 min in `src/Modules/Organization/Organization.Infrastructure/Services/ManagementHierarchyService.cs`
- [x] T021 [US1] Implement `HierarchyCacheInvalidator` (`IIntegrationEventHandler<OrganizationHierarchyChangedIntegrationEvent>`) deleting affected keys on hierarchy change in `src/Modules/Organization/Organization.Infrastructure/Services/HierarchyCacheInvalidator.cs`
- [x] T022 [US1] Add Redis fallback: on `RedisConnectionException`, log warning and execute CTE directly (never fail authorization) in `src/Modules/Organization/Organization.Infrastructure/Services/ManagementHierarchyService.cs` (extend T020)
- [x] T023 [US1] Create EF migration via `dotnet ef migrations add AddManagementHierarchy --project src/Modules/Organization/Organization.Infrastructure/Organization.Infrastructure.csproj --startup-project src/Api/Api.csproj` in `src/Modules/Organization/Organization.Infrastructure/Migrations/`
- [x] T024 [US1] Implement `AssignManager` command (IBusinessRule `CheckRule` + `ManagerAssignedToUser` → `OrganizationHierarchyChangedIntegrationEvent` via outbox + cache invalidation) in `src/Modules/Organization/Organization.Application/Features/AssignManager/AssignManagerCommand.cs` + Validator + Handler + `IEndpoint` (`POST /api/organization/relationships`)
- [x] T025 [US1] Implement `MoveOrganizationUnit`, `IssueExplicitGrant`, `RevokeExplicitGrant` commands in `src/Modules/Organization/Organization.Application/Features/MoveOrganizationUnit/` and `Features/ExplicitGrant/` (each with Validator/Handler/Endpoint)
- [x] T026 [US1] Implement queries `GetSubtree`, `WhoReportsToMe`, `GetAncestors` (`IQuery<Result<IReadOnlyList<Guid>>>`) delegating to `IManagementHierarchy` in `src/Modules/Organization/Organization.Application/Features/GetSubtree/` etc. (endpoints `GET /api/organization/managers/{id}/subtree`, `/api/organization/users/{id}/ancestors`)

**Checkpoint**: At this point, User Story 1 is fully functional — hierarchy is cycle-free, subtree queries are cache-aware, grants respect expiry, and migrations are applied

---

## Phase 4: User Story 2 - System enforces hierarchical authorization on resource access (Priority: P1)

**Goal**: Single `IAuthorizationEvaluator` composing Golden Rule A; every list/search/dashboard composes a subtree `Specification<T>` before fetch; cross-branch isolation with only explicit-grant/project-membership exceptions

**Independent Test**: With frozen hierarchy Root→ManagerA→{A1, A2, M-A1→reports} and ManagerB in another branch, `ManagerA` task query returns subtree+grant+member set, `ManagerB` query for same set returns empty (not 403 that leaks existence), `Contributor` without grant outside subtree returns empty; a `Specification<T>` test proves `SubtreeSpecification` is `And`ed before `ToListAsync`

### Tests for User Story 2

- [x] T027 [P] [US2] Unit test `AuthorizationEvaluator` composition matrix in `tests/Organization.Tests/Domain/AuthorizationEvaluatorTests.cs` (tenant mismatch → TenantMismatch; missing permission → MissingPermission; not in subtree → NotInSubtree; grant covering → Allow; member covering → Allow; classification denied → ClassificationDenied; deny reason not leaked to caller)
- [x] T028 [P] [US2] Unit test `SubtreeSpecification<T>.IsSatisfiedBy` and composition via `And` in `tests/Organization.Tests/Domain/SubtreeSpecificationTests.cs` (owner in subtree → true; owner in other branch → false; tenant mismatch → false)
- [x] T029 [P] [US2] Integration test subtree-filtered task query against seeded 1,000-user hierarchy in `tests/Organization.Tests/Integration/SubtreeFilteredQueryTests.cs` (A sees exactly subtree+grant+member items, B sees empty, p95 <500 ms)

### Implementation for User Story 2

- [x] T030 [US2] Seed permission catalog and role→permission map via `IHostedService` (`PermissionSeederHostedService`) reading `PermissionsSeed.json` or code list in `src/Modules/Identity/Identity.Infrastructure/Seed/PermissionCatalogSeed.cs` + `RolePermissionSeed.cs` (covers 10 roles, 16 permissions per `contracts/permission-catalog-contract.md`)
- [x] T031 [US2] Implement `IPermissionCatalog` (`HasPermissionAsync`, `GetPermissionsAsync`) backed by `EfRepository<Permission>` + `RolePermission` with 10-min Redis cache in `src/Modules/Identity/Identity.Infrastructure/Services/PermissionCatalogService.cs` and contract `Identity.Contracts/IPermissionCatalog.cs`
- [x] T032 [US2] Implement `AuthorizationEvaluator` (`IAuthorizationEvaluator`) with fixed order tenant→permission→ownership→subtree→grant→membership→classification, generic `Forbidden` to caller + internal `DenyReason` for audit, in `src/Modules/Organization/Organization.Infrastructure/Services/AuthorizationEvaluator.cs`
- [x] T033 [US2] Implement `SubtreeSpecification<T>` (`Specification<T>` filtering by `ownerId IN subtree ∪ {actorId}` + `tenant_id == TenantId`) in `src/Modules/Organization/Organization.Infrastructure/Specifications/SubtreeSpecification.cs` (composable via `And`)
- [x] T034 [US2] Implement `IProjectMembership` stub (returns false until Projects spec) in `src/Modules/Organization/Organization.Infrastructure/Services/ProjectMembershipStub.cs` consumed by evaluator
- [x] T035 [US2] Implement policy probe `CanActorPerform` query (`AuthorizationRequest → AuthorizationResult`, test/UI probe) in `src/Modules/Organization/Organization.Application/Features/CanActorPerform/CanActorPerformQuery.cs` + `IEndpoint` (`POST /api/authorization/can-perform`)
- [x] T036 [US2] Wire evaluator into every list/search/dashboard handler: document the pattern — each handler resolves `IAuthorizationEvaluator` or composes `SubtreeSpecification<T>` before `repository.ListAsync` — and add a guarded example in `docs/authorization/subtree-specification-usage.md` referencing `SubtreeSpecificationTests`
- [x] T037 [US2] Add classification check to evaluator (if `Classification` supplied, deny `HighlyRestricted` without clearance) in `src/Modules/Organization/Organization.Infrastructure/Services/AuthorizationEvaluator.cs` (extend T032)

**Checkpoint**: At this point, User Stories 1 AND 2 work together — hierarchy plus Golden Rule A evaluator with subtree Specification, grant/membership exceptions, and classification

---

## Phase 5: User Story 3 - Auditor and manager observe authorization decisions and hierarchy changes (Priority: P2)

**Goal**: Every deny is audited via outbox with actor/resource/permission/tenant/correlationId; hierarchy changes invalidate caches; grant expiry is enforced on every check

**Independent Test**: Trigger `CanActorPerform` deny for B on A's task, then query `audit.authorization.denied` store and assert entry contains actor, resource type/id, permission, tenant, correlationId; then change hierarchy (new subordinate D to A), assert `GetSubtree(A)` count increments without restart (cache invalidation); then `CanActorPerform` with expired grant returns Deny

### Tests for User Story 3

- [x] T038 [P] [US3] Unit test grant expiry edge in `tests/Organization.Tests/Domain/ExplicitGrantExpiryTests.cs` (past `ExpiresAt` → `IsSatisfiedBy` false, future → true, null → true; `CanActorPerform` with expired grant → `GrantExpired` deny)
- [x] T039 [P] [US3] Integration test audited deny via outbox in `tests/Organization.Tests/Integration/AuditedDenyTests.cs` (trigger `CanActorPerform` deny, then `audit` table/Domain Event contains `authorization.denied` with actor/resource/permission/tenant/correlationId same transaction)
- [x] T040 [P] [US3] Integration test cache invalidation on hierarchy change in `tests/Organization.Tests/Integration/HierarchyCacheInvalidationTests.cs` (cache `GetSubtree(A)`, then `AssignManager(A→D)`, then `GetSubtree(A)` repopulates with D; empty subtree for manager with no reports returns `[]` not null)

### Implementation for User Story 3

- [x] T041 [US3] Emit `audit.authorization.denied` from `AuthorizationEvaluator` on every `Deny` via `RaiseDomainEvent`/`IOutboxWriter` capturing actor/resource/permission/tenant/correlationId (`Activity.Current?.TraceId` or `IHttpContextAccessor.TraceIdentifier`) in `src/Modules/Organization/Organization.Infrastructure/Services/AuthorizationEvaluator.cs` (extend T032)
- [x] T042 [US3] Implement `OrganizationHierarchyChangedIntegrationEvent` publishing from `ManagementRelationship` domain events (`ManagerAssignedToUser` → outbox → `IntegrationEvent`) in `src/Modules/Organization/Organization.Infrastructure/Events/ManagerAssignedToUserHandler.cs` (transactional outbox pattern per BuildingBlocks)
- [x] T043 [US3] Ensure `ExplicitGrant` expiry is evaluated on every `CanActorPerform` call (call `IsExpired(DateTime.UtcNow)` before `IsSatisfiedBy`) in `src/Modules/Organization/Organization.Infrastructure/Services/AuthorizationEvaluator.cs` (extend T032 — already covered but make explicit)
- [x] T044 [US3] Add `WhoReportsToMe` / `GetSubtree` empty-result handling (return `Array.Empty<Guid>()` never null, no cache poisoning) in `src/Modules/Organization/Organization.Application/Features/GetSubtree/GetSubtreeQuery.cs` and `src/Modules/Organization/Organization.Infrastructure/Services/ManagementHierarchyService.cs`

**Checkpoint**: All three user stories are independently functional — hierarchy, authorization, and audit/cache observability

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Security matrix, performance, docs, and quickstart validation spanning all stories

- [x] T045 Security matrix tests for `ManagementRelationship` and `ExplicitGrant` covering 8 actor types (Owner/Manager/Manager'sManager/Peer/DifferentBranch/Auditor/Admin/Anonymous) with explicit expected allow/deny per `spec` TDD Strategy in `tests/Organization.Tests/Security/AuthorizationMatrixTests.cs`
- [x] T046 Extend `tests/Architecture/ArchitectureTests.cs` with hierarchy boundary checks (no direct DbContext access from other modules, `IManagementHierarchy` is the only hierarchy query path via `Organization.Contracts`)
- [x] T047 Performance smoke: seed 1,000-user hierarchy and assert `IsInSubtree`/`GetSubtree` p95 <50 ms warm / task list with subtree <500 ms p95 in `tests/Organization.Tests/Integration/HierarchyPerformanceTests.cs`
- [x] T048 Run `specs/003-identity-access-organization/quickstart.md` steps 1–5 (migrations, permission seed, hierarchy cycle/subtree, Golden Rule A + cross-branch, tenant isolation, audit/cache) and fix deviations in `src/Modules/Organization/Organization.Infrastructure/` / `src/Api/`
- [x] T049 Update `docs/adr/adr-004-hierarchy-storage.md` with final CTE + index choices and benchmark results from T047
- [x] T050 Update `README.md` with identity & organization run instructions (Authority via `oroidentityserver` discovery, `tenant_id` propagation, `IManagementHierarchy` Shared Kernel note) referencing `draft/discovery/000-repository-catalog.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1) and US2 (P1) can proceed in parallel for planning, but US2's evaluator consumes `IManagementHierarchy` from US1 — so implement US1's `ManagementHierarchyService` (T020) + Redis invalidation (T021) before US2's evaluator `Subtree` step (T032/T033)
  - US3 (P2) depends on US1 (hierarchy changes to audit/invalidate) and US2 (deny decisions to audit)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational — no other story dependencies
- **User Story 2 (P1)**: After Foundational + T020/T021 (hierarchy service + cache) — otherwise independent; can overlap with US1's command/query wiring
- **User Story 3 (P2)**: After US1 (changes to observe) and US2 (denies to audit) — may start writing unit tests earlier but final assertion needs prior stories' code

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation (TDD)
- Value objects / StronglyTypedIds before aggregates
- Aggregates + IBusinessRules before handlers
- Handlers before endpoints (`IEndpoint`)
- Migrations after DbContext/Configurations
- Story complete before moving to next priority

### Parallel Opportunities

- T005 + T006 (value objects) + T007 (errors) can run in parallel — different files
- T011–T013 (US1 unit tests) can run in parallel — different test classes
- T015–T017 (aggregates) can run in parallel — different aggregate files
- T020–T022 (hierarchy service + invalidation + fallback) can overlap once T018 is done
- T027–T029 (US2 unit/integration tests) can run in parallel
- T038–T040 (US3 tests) can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all US1 unit tests together (different files):
Task: "Unit test SubtreeCannotContainManagerRule in tests/Organization.Tests/Domain/SubtreeCannotContainManagerRuleTests.cs"  # T011
Task: "Unit test single-active-per-unit in tests/Organization.Tests/Domain/ManagementRelationshipTests.cs"                # T012
Task: "Unit test ExplicitGrant.IsSatisfiedBy expiry in tests/Organization.Tests/Domain/ExplicitGrantTests.cs"            # T013

# Launch aggregates together:
Task: "Implement ManagementRelationship aggregate in src/Modules/Organization/Organization.Domain/Aggregates/ManagementRelationship.cs"  # T015
Task: "Implement OrganizationUnit aggregate in src/Modules/Organization/Organization.Domain/Aggregates/OrganizationUnit.cs"          # T016
Task: "Implement ExplicitGrant aggregate in src/Modules/Organization/Organization.Domain/Aggregates/ExplicitGrant.cs"                  # T017
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T010) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T011–T026)
4. **STOP and VALIDATE**: Seed A→B→C, verify `GetAncestors(C)` contains A,B, `IsInSubtree(A,C)` true, `AssignManager(C→A)` rejected, cached `GetSubtree(A)` repopulates after new assignment
5. Demo the hierarchy without requiring evaluator — proves Shared Kernel is solid

### Incremental Delivery

1. Setup + Foundational → contracts and contexts ready
2. + US1 → cycle-free hierarchy + cache-aware subtree → MVP
3. + US2 → Golden Rule A evaluator + SubtreeSpecification + grant/membership → cross-branch isolation
4. + US3 → audited denials + cache invalidation + grant expiry → observability
5. Each increment adds value without breaking previous

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 — aggregates (T015–T017) + hierarchy service + cache (T020–T022) + commands (T024–T026)
   - Developer B: US1 — value objects/Ids (T014) + configurations/migrations (T018–T023)
   - Developer C: US2 — permission seed (T030–T031) + evaluator + SubtreeSpecification (T032–T034) — can start after T020
3. US1 merges first (blocks US3's final assertion); US2 and US3 converge; Polish is joint validation

---

## Notes

- [P] tasks = different files, no dependencies — safe to parallelize
- [Story] label maps task to user story for traceability to FR-001…FR-009 and SC-001…SC-005
- Each user story is independently testable per its Independent Test criterion
- Tests are written FIRST and must fail before implementation (TDD, Principle XXI)
- Grant expiry and tenant mismatch are explicit edge cases — test them in unit, not only integration
- Avoid: hard-coded permissions (violates FR-002), recursive hierarchy without cycle check (violates FR-003), filtering after fetch (violates FR-006), leaking deny reasons to callers (violates FR-005)

---

## Phase 7: Convergence

**Purpose**: Close remaining gaps between spec/plan intent and current code for identity configuration of Api and Angular Web (user-reported: "no veo que se realice una configuracion de identity para el api y la aplicacion angular web")

- [x] T051 Fix Web OIDC client registration to use OroKanban client per FR-001 (partial) — replace `clientId: 'quizarena-player'` with `orokanban-api` (or `orokanban-web`) and align `scope: 'openid profile email offline_access'` with `oroidentityserver` `POST /api/applications` registration (authorization_code + refresh_token) in `src/Web/src/app/app.config.ts` — currently `src/Web/src/app/app.config.ts:14` uses `quizarena-player`
- [x] T052 Wire hierarchical authorization policies in Api per FR-005/R5 and SC-002 (partial) — extend `src/Api/Program.cs` `AddAuthorization` to register policies that delegate to `IAuthorizationEvaluator` (Golden Rule A) and protect `Organization` endpoints with `RequireAuthorization`, ensuring every list/search/dashboard composes `SubtreeSpecification<T>` before fetch — currently `Program.cs:56` calls `AddAuthorization()` with no policies
- [x] T053 Align Web environment configuration with Aspire service discovery per plan: storage decision (partial) — make `src/Web/src/app/environments/environment.ts` derive `apiUrl` and `identityAuthority` from `import.meta.env`/`window.__env` injected via `OroKanban.AppHost` `WithEnvironment("API_URL", api.GetEndpoint("http"))` and `IDENTITY_AUTHORITY` instead of hard-coded `http://localhost:5080` and `/api` — currently `environment.ts:3` hard-codes `apiUrl: '/api'`
- [x] T054 Propagate and verify `tenant_id` claim from `oroidentityserver` `/connect/userinfo` through Api per FR-001/SC-001 (partial) — ensure `TenantClaimsTransformation` (already present) is covered by an integration test that calls a protected endpoint with a real OIDC token and asserts `TenantContext.TenantId` and `sub` are available, and document the `POST /api/applications` client registration for `orokanban-web` — currently no test in `tests/Organization.Tests/Integration` covers OIDC claim mapping
- [x] T055 Review and remove unrequested `quizarena-player` branding from Web per plan (unrequested) — audit `src/Web/src/app/` for `quizarena` strings (`clientId`, Dockerfile comments referencing `quizarena-player`, `WithEnvironment("PORT")` mismatch) and replace with `orokanban` naming or justify via ADR — currently `src/Web/src/app/app.config.ts:14` and `OroKanban.AppHost/AppHost.cs:71` reference quizarena
