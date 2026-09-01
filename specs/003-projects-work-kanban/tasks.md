# Tasks: Projects, Work Items and Kanban

**Input**: Design documents from `/specs/003-projects-work-kanban/` (spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md)
**Branch**: `003-projects-work-kanban` | **Date**: 2026-09-01
**Tech stack**: C# .NET 10, BuildingBlocks (Kernel.Domain/CQRS/EventBus.RabbitMQ/Kernel.Infrastructure/ServiceDefaults), Npgsql/PostgreSQL (schema `projects`), Redis (via IManagementHierarchy), xUnit + Testcontainers + NSubstitute

**Tests**: Constitution Principle XX mandates authorization + domain rule coverage. The feature spec explicitly requires unit (transition map exhaustive, cycle, assignment matrix, reparenting, VO, concurrency), integration (hierarchy CTE, board query with auth, outbox, 409), and E2E (Kanban drag/drop round-trip). All test tasks below are therefore required and MUST be written FIRST and FAIL before implementation (TDD).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify scaffolding, create test project, and wire module references — no domain code yet.

- [X] T001 Verify Projects module scaffolding exists per plan.md project structure in `src/Modules/Projects/Projects.Domain/`, `Projects.Application/`, `Projects.Infrastructure/`, `Projects.Contracts/` (4 classlibs `net10.0`) and `src/Api/Api.csproj` composition host; record `dotnet --version` and `dotnet sln OroKanban.slnx list` in commit
- [X] T002 Create xUnit test project `tests/Projects.Tests/Projects.Tests.csproj` (net10.0, refs: Projects.Domain, Projects.Application, Projects.Infrastructure, Projects.Contracts, BuildingBlocks.Kernel.Domain, xUnit, NSubstitute, FluentAssertions, Testcontainers.PostgreSql, Testcontainers.Redis) and add to `OroKanban.slnx` via `dotnet sln add`
- [X] T003 [P] Add package references to Projects modules per plan.md in `Directory.Packages.props` (Npgsql.EntityFrameworkCore.PostgreSQL, Microsoft.EntityFrameworkCore) and verify `dotnet build OroKanban.slnx -warnaserror` passes with 0 warnings
- [X] T004 [P] Configure `OroKanban.AppHost/AppHost.cs` to ensure `postgres` with database `orokanban` and `redis` are referenced by `api` (already from 002 — verify `WithReference(postgres).WaitFor(postgres)` and `WithReference(redis)` present; no new resource required) and `dotnet run --project OroKanban.AppHost` shows Healthy dashboard
- [X] T005 [P] Create `specs/003-projects-work-kanban/contracts/` contracts index file `specs/003-projects-work-kanban/contracts/README.md` linking the 4 contracts (projects-api, workitems-api, kanban-board, domain-events) for traceability

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: StronglyTypedIds, Enumerations, ValueObjects, ProjectsDbContext + outbox, shared-kernel consumption, and enumeration seeding — MUST complete before ANY user story. Unblocks all 6 stories.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create StronglyTypedIds `ProjectId`, `WorkItemId`, `WorkItemDependencyId` in `src/Modules/Projects/Projects.Domain/Ids/ProjectIds.cs` (sealed records `: StronglyTypedId<Guid>`)
- [X] T007 [P] Create Enumerations `WorkItemType`, `WorkItemStatus`, `WorkItemPriority`, `Criticality`, `DependencyType`, `ProjectStatus`, `ProjectPriority`, `ProjectRole` in `src/Modules/Projects/Projects.Domain/Enumerations/` (each `: Enumeration` with `Backlog(1)..Completed(6)` etc. per data-model.md §4; `WorkItemStatus` exposes `Id` only — map lives in Policy)
- [X] T008 [P] Create ValueObjects `Effort`, `ProgressValue`, `DueDate`, `Tag` in `src/Modules/Projects/Projects.Domain/ValueObjects/` (validated at construction per data-model.md §5: Effort `>=0 precision 0.1`, Progress `0..100`, DueDate nullable UTC, Tag `1..50 ^[a-z0-9_-]+$` trimmed/lowercased/deduped)
- [X] T009 [P] Create `ProjectsDbContext : AppDbContextBase` in `src/Modules/Projects/Projects.Infrastructure/Persistence/ProjectsDbContext.cs` with `HasDefaultSchema("projects")`, `ApplyConfiguration(new OutboxEntityTypeConfiguration())`, DbSets `Projects`, `WorkItems`, `WorkItemDependencies`, and Npgsql `IsRowVersion()` mapping for `WorkItem.RowVersion` + `Project.RowVersion`
- [X] T010 [P] Create EF configurations `ProjectConfiguration`, `WorkItemConfiguration`, `WorkItemDependencyConfiguration`, `EnumerationConfiguration` in `src/Modules/Projects/Projects.Infrastructure/Persistence/Configurations/` (tables `projects.projects`, `projects.work_items` with self-FK `parent_id` indexed `(project_id,parent_id)`, `projects.work_item_dependencies` with unique `(dependent_id,principal_id)` + `type` check, owned `Tag` collection as `projects.work_item_tags` join, enumeration seed `HasData`)
- [X] T011 Create enumeration seeder `ProjectsEnumerationSeeder` in `src/Modules/Projects/Projects.Infrastructure/Seed/ProjectsEnumerationSeeder.cs` (IHostedService or `HasData` fallback — seeds `WorkItemType Epic/Feature/Task/Subtask`, `WorkItemStatus 6`, `WorkItemPriority 5`, `Criticality 4`, `DependencyType 4`, `ProjectStatus 5`, `ProjectRole 4`)
- [X] T012 Create domain services contracts `IDependencyCycleDetector`, `IAssignmentPolicy`, `IWorkItemTransitionPolicy`, `IHierarchyInspector` in `src/Modules/Projects/Projects.Domain/Services/` (interfaces per data-model.md §6) and `IProjectMembership` in `src/Modules/Projects/Projects.Contracts/IProjectMembership.cs`
- [X] T013 [P] Create business rule placeholders `TransitionIsAllowedRule`, `CircularDependencyRule`, `ReparentNoCycleRule`, `WorkItemNotCompletedRule` in `src/Modules/Projects/Projects.Domain/Rules/` (`: IBusinessRule` with `IsBroken()` and `Message`)
- [X] T014 Register Projects module DI in `src/Modules/Projects/Projects.Infrastructure/DependencyInjection.cs` (`AddProjectsModule(IServiceCollection)` — registers `ProjectsDbContext` with Npgsql, `EfRepository<>`, `IUnitOfWork`, `IOutboxWriter`, `IDependencyCycleDetector`, `IWorkItemTransitionPolicy`, `IHierarchyInspector`) and wire in `src/Api/Program.cs` via `builder.Services.AddProjectsModule()` and `AddCqrs` + `AddEndpoints(typeof(Projects.Domain.AssemblyReference).Assembly)`
- [X] T015 Create EF migration `Projects_003_Initial` via `dotnet ef migrations add Projects_003_Initial --project src/Modules/Projects/Projects.Infrastructure --startup-project src/Api/Api.csproj` and verify `dotnet ef database update` creates schema `projects` and tables `projects.projects`, `projects.work_items`, `projects.work_item_dependencies`, `outbox_messages`

**Checkpoint**: Foundation ready — `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `dotnet ef database update` creates `projects` schema, enumerations seeded. User stories can now begin.

---

## Phase 3: User Story 1 — Project and work item creation with hierarchy (Priority: P1) 🎯 MVP

**Goal**: Manager creates a project, adds participants, and creates typed hierarchical work items (Epic→Feature→Task→Subtask via `ParentId`) — the data foundation for all later stories.

**Independent Test**: Create Project (owner+manager) → AddProjectMember ×2 → Create Epic → Create Feature with ParentId=Epic → Create Task with ParentId=Feature; verify each persists with Version 1, raises its `*Created` domain event via outbox, `ParentId` linkage survives `GetWorkItemDetail`, and `WorkItemType` outside Enumeration is rejected (spec US1 scenarios 1–5).

### Tests for User Story 1 (write FIRST, ensure FAIL)

- [X] T016 [P] [US1] Unit test `ProjectAggregateTests` in `tests/Projects.Tests/Unit/ProjectAggregateTests.cs` — `Project.Create` validates name/priority/criticality, raises `ProjectCreated`, enforces `ProjectMember` unique per `(ProjectId,UserId)`, duplicate add returns `Error.Validation`
- [X] T017 [P] [US1] Unit test `WorkItemTypeEnumerationTests` in `tests/Projects.Tests/Unit/WorkItemTypeEnumerationTests.cs` — all 4 seeded types resolve, unknown type `Result` is `Error.Validation`, taxonomy can add a new value without aggregate change (add seed row, resolve)
- [X] T018 [P] [US1] Unit test `ValueObjectTests` in `tests/Projects.Tests/Unit/ValueObjectTests.cs` — `Effort` negative rejected, `ProgressValue` 101 rejected / 0–100 ok, `Tag` empty/long/invalid-char rejected with normalization (trim+lowercase), `DueDate` IsOverdue rule
- [ ] T019 [P] [US1] Integration test `ProjectPersistenceTests` in `tests/Projects.Tests/Integration/ProjectPersistenceTests.cs` — Testcontainers Postgres: `CreateProject` → `ProjectsDbContext` persists with RowVersion, outbox contains `ProjectCreatedIntegrationEvent`; `AddProjectMember` emits `ProjectMemberAdded`; `IProjectMembership.IsMember` returns true
- [ ] T020 [P] [US1] Integration test `WorkItemHierarchyPersistenceTests` in `tests/Projects.Tests/Integration/WorkItemHierarchyPersistenceTests.cs` — create Epic→Feature→Task chain via `ParentId` in same `ProjectId`; verify `GetWorkItemDetail` returns ancestry; attempt `CreateWorkItem` with `ParentId` in different `ProjectId` → 400; arbitrary depth (5 levels) succeeds
- [ ] T021 [P] [US1] Contract test `ProjectsApiContractTests` in `tests/Projects.Tests/Contract/ProjectsApiContractTests.cs` — `POST /api/projects` envelope (201 + Location + version 1), `POST /api/projects/{id}/members` (200, duplicate 400), `POST /api/projects/{id}/workitems` (201 with Backlog + version 1) per `contracts/projects-api-contract.md` + `contracts/workitems-api-contract.md`

### Implementation for User Story 1

- [X] T022 [P] [US1] Implement `Project` aggregate in `src/Modules/Projects/Projects.Domain/Aggregates/Project.cs` (`AggregateRoot<ProjectId>` with `Create(name,tenantId,ownerId,managerId,status,priority,criticality)` → `CheckRule` name not empty + unique-per-tenant invariant (handled by unique index), `AddMember(userId,role)`/`RemoveMember`, `ChangeStatus`, `AddMilestone`, raise domain events, `Version`+`RowVersion`)
- [X] T023 [P] [US1] Implement `ProjectMember` and `Milestone` entities in `src/Modules/Projects/Projects.Domain/Entities/` (owned by Project, `ProjectRole` Enumeration, `JoinedAt`, milestone `IsReached` logic)
- [X] T024 [P] [US1] Implement `WorkItem` aggregate create path in `src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs` (`AggregateRoot<WorkItemId>` — `Create(projectId,tenantId,title,type,priority,criticality,parentId,ownerId,responsibleId,reviewerId,dueDate,effort,tags,progress)` with VO validation, `ParentId` not self + same-project check placeholder, initial `Status=WorkItemStatus.Backlog`, `Version=1`, `RowVersion` set, raise `WorkItemCreated`)
- [X] T025 [US1] Implement vertical slices `CreateProject`, `AddProjectMember` in `src/Modules/Projects/Projects.Application/Features/Projects/` — each with `CreateProjectCommand : ICommand<Result<CreateProjectResponse>>` + `Validator` (RuleFor name 3–200, status/priority in Enumeration, tenant from `TenantContext`) + `Handler` (evaluator `project.create` check, `IRepository<Project>`, `IOutboxWriter`, `IUnitOfWork.SaveChangesAsync`) + `CreateProjectEndpoint : IEndpoint` (`MapPost /api/projects` → `Result.ToCreatedResult`)
- [X] T026 [US1] Implement vertical slices `CreateWorkItem` in `src/Modules/Projects/Projects.Application/Features/WorkItems/CreateWorkItem/` (same pattern: `CreateWorkItemCommand` + `CreateWorkItemValidator` + `CreateWorkItemHandler` + `CreateWorkItemEndpoint` `POST /api/projects/{projectId}/workitems`; handler validates `WorkItemType` is seeded enumeration, `ParentId` belongs to same `ProjectId` via `IRepository<WorkItem>.ExistsAsync`, loads project to check tenant, calls `WorkItem.Create`, stages `WorkItemCreatedIntegrationEvent` via `IOutboxWriter`)
- [X] T027 [US1] Implement read slices `GetProjectDetail`, `GetWorkItemDetail` in `src/Modules/Projects/Projects.Application/Features/Projects/GetProjectDetail/` and `Features/WorkItems/GetWorkItemDetail/` (`IQuery<Result<DetailResponse>>` + `Handler` composing tenant-filtered `Specification` + `AuthorizedWorkItemSpec` placeholder (full auth wiring deferred to US4) + mapping to `Projects.Contracts/Dtos/` DTOs)
- [X] T028 [US1] Implement DTOs `ProjectDetailResponse`, `CreateProjectResponse`, `WorkItemDetailResponse`, `WorkItemSummary` in `src/Modules/Projects/Projects.Contracts/Dtos/` (never domain entities, per Principle XVI)
- [X] T029 [US1] Implement integration events `ProjectCreatedIntegrationEvent`, `ProjectMemberAddedIntegrationEvent`, `WorkItemCreatedIntegrationEvent` in `src/Modules/Projects/Projects.Contracts/Events/` (`: IntegrationEvent`)

**Checkpoint**: US1 independently testable — `dotnet test --filter US1` passes; `POST /api/projects` + `POST /api/projects/{id}/workitems` with hierarchy E2E works via Testcontainers; board shows Backlog item.

---

## Phase 4: User Story 2 — Kanban board and validated state transitions (Priority: P1)

**Goal**: Team member drags cards between status columns; domain validates `WorkItemStatus` transition map via `TransitionIsAllowedRule` + `IWorkItemTransitionPolicy`, authorized via SPEC-002 evaluator, audited, and the board re-renders in new column — or rejection leaves board unchanged.

**Independent Test**: Seed WorkItem in Backlog → `ChangeWorkItemStatus` to Planned (allowed) → verify `WorkItemStatusChanged` + audit + board shows Planned; → `ChangeWorkItemStatus` Backlog→Completed (disallowed) → 400 Validation `Transition not allowed`, no event, board still Backlog (spec US2 scenarios 1–5, SC-002).

### Tests for User Story 2 (write FIRST)

- [X] T030 [P] [US2] Unit test exhaustive transition map `WorkItemTransitionMapTests` in `tests/Projects.Tests/Unit/WorkItemTransitionMapTests.cs` — 6×6=36 pairs: assert allowed pairs per research Decision 2 map and that all other pairs are rejected; uses `[Theory][MemberData(nameof(AllPairs))]` and `IWorkItemTransitionPolicy.IsAllowed`
- [X] T031 [P] [US2] Unit test `TransitionIsAllowedRuleTests` in `tests/Projects.Tests/Unit/TransitionIsAllowedRuleTests.cs` — `CheckRule` with `TransitionIsAllowedRule(current,target,policy)` returns IsBroken=false for allowed, true for disallowed; plus reopen rules `Completed→InProgress` allowed vs `Completed→Backlog` manager-only
- [X] T032 [P] [US2] Unit test `WorkItemStatusEnumerationTests` in `tests/Projects.Tests/Unit/WorkItemStatusEnumerationTests.cs` — 6 seeded statuses resolve, enumeration Id stable
- [ ] T033 [P] [US2] Integration test `ChangeWorkItemStatusIntegrationTests` in `tests/Projects.Tests/Integration/ChangeWorkItemStatusIntegrationTests.cs` — Testcontainers: Backlog→Planned succeeds (Version 1→2, outbox `WorkItemStatusChangedIntegrationEvent`, audit entry); Backlog→Completed returns `Error.Validation`; `InProgress→Blocked` and `Blocked↔InReview` succeed; unauthorized actor → `Error.Forbidden` + `authorization.denied` audit (evaluator stub via NSubstitute for `IAuthorizationEvaluator`)
- [ ] T034 [P] [US2] Contract test `WorkItemsStatusApiContractTests` in `tests/Projects.Tests/Contract/WorkItemsStatusApiContractTests.cs` — `POST /api/workitems/{id}/status` envelope per `contracts/workitems-api-contract.md` (200 on allowed, 400 validation, 403 generic denial without leak, 409 on stale Version, 404)

### Implementation for User Story 2

- [X] T035 [P] [US2] Implement `IWorkItemTransitionPolicy` in `src/Modules/Projects/Projects.Infrastructure/Services/WorkItemTransitionPolicy.cs` (`ReadOnlyDictionary<int,IReadOnlySet<int>>` per research Decision 2, inject `IOptions<WorkItemTransitionOptions>` for reopen rules; methods `IsAllowed(from,to)` and `AllowedFrom(from)`)
- [X] T036 [P] [US2] Implement `TransitionIsAllowedRule` in `src/Modules/Projects/Projects.Domain/Rules/TransitionIsAllowedRule.cs` (`: IBusinessRule`, ctor `(WorkItemStatus current, WorkItemStatus target, IWorkItemTransitionPolicy policy)`, `IsBroken() => !policy.IsAllowed(current,target)`)
- [X] T037 [US2] Implement `WorkItem.ChangeStatus(target, policy, actor)` domain method in `src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs` (calls `CheckRule(new TransitionIsAllowedRule(Status,target,policy))`, sets `Status=target`, increments `Version`, raises `WorkItemStatusChangedDomainEvent`, sets `CompletedAt` when `→Completed`, `WorkItemBlocked` when blocked derivation true)
- [X] T038 [US2] Implement vertical slice `ChangeWorkItemStatus` in `src/Modules/Projects/Projects.Application/Features/WorkItems/ChangeWorkItemStatus/` (`ChangeWorkItemStatusCommand(workItemId,targetStatus,expectedVersion)` + `Validator` (target in Enumeration, version >0) + `Handler` (load WorkItem with RowVersion, check `expectedVersion==Version`, `IAuthorizationEvaluator.CanActorPerform(actor,workitem,workitem.update)` → `Error.Forbidden`+outbox `authorization.denied` if denied, else `workItem.ChangeStatus`, `IRepository.Update`, stage `WorkItemStatusChangedIntegrationEvent`, same-tx audit via outbox) + `ChangeWorkItemStatusEndpoint` `POST /api/workitems/{id}/status` → `Result.ToHttpResult` 400/403/409)
- [X] T039 [US2] Implement `CompleteWorkItem` convenience slice in `src/Modules/Projects/Projects.Application/Features/WorkItems/CompleteWorkItem/` (delegates to `ChangeStatus(Completed)` + sets `ActualEffort` if provided; raises `WorkItemCompleted`)
- [X] T040 [US2] Implement status-related domain events `WorkItemStatusChangedDomainEvent`, `WorkItemCompletedDomainEvent`, `WorkItemBlockedDomainEvent` in `src/Modules/Projects/Projects.Domain/Events/` and their `IntegrationEvent` counterparts in `src/Modules/Projects/Projects.Contracts/Events/`

**Checkpoint**: US2 independently testable — exhaustive 36-pair suite green, allowed transitions move column, disallowed returns 400 and board unchanged.

---

## Phase 5: User Story 3 — Hierarchy reparenting and dependency management with cycle prevention (Priority: P2)

**Goal**: Lead reorganizes work by reparenting items and linking dependencies; system prevents hierarchy cycles (descendant check via recursive CTE) and dependency cycles (`CircularDependencyRule` via `IDependencyCycleDetector` DFS filtering `RelatedTo`).

**Independent Test**: Chain A→B→C `Blocks` → attempt C→A → 400 `Circular dependency` and graph unchanged; reparent Task from Epic-1 to Epic-2 → `WorkItemReparented` and new ParentId; reparent Epic under its own descendant → 400 (spec US3 scenarios 1–5, SC-003).

### Tests for User Story 3 (write FIRST)

- [X] T041 [P] [US3] Unit test `DependencyCycleDetectorTests` in `tests/Projects.Tests/Unit/DependencyCycleDetectorTests.cs` — pure `IDependencyCycleDetector.HasCycle` on chains, diamonds, self-loop, 100-node graph <200 ms, `RelatedTo` filtered out (long RelatedTo chain no cycle), `BlockedBy` inverse equivalence
- [X] T042 [P] [US3] Unit test `ReparentNoCycleRuleTests` in `tests/Projects.Tests/Unit/ReparentNoCycleRuleTests.cs` — newParent is descendant → IsBroken=true; same-project check; unrelated parent in same project → false; uses `IHierarchyInspector` stub
- [X] T043 [P] [US3] Unit test `WorkItemDependencyAggregateTests` in `tests/Projects.Tests/Unit/WorkItemDependencyAggregateTests.cs` — `WorkItemDependency.Create` validates `dependent!=principal`, stage `DependencyAdded` domain event
- [ ] T044 [P] [US3] Integration test `WorkItemDependencyIntegrationTests` in `tests/Projects.Tests/Integration/WorkItemDependencyIntegrationTests.cs` — Testcontainers: A Blocks B, B Blocks C persists; C→A rejected 400 with `CircularDependencyRule`, graph remains 2 edges; `RelatedTo` C→A succeeds 201; cross-project Blocks rejected unless RelatedTo with policy (default deny)
- [ ] T045 [P] [US3] Integration test `ReparentIntegrationTests` in `tests/Projects.Tests/Integration/ReparentIntegrationTests.cs` — Testcontainers Postgres with recursive CTE: reparent Task Epic-1→Epic-2 emits `WorkItemReparented`, new ParentId via `GetWorkItemDetail`; reparent Epic under descendant rejected; null parent (root promotion) succeeds; racing concurrent reparent relies on same RowVersion path

### Implementation for User Story 3

- [X] T046 [P] [US3] Implement `WorkItemDependency` aggregate in `src/Modules/Projects/Projects.Domain/Aggregates/WorkItemDependency.cs` (`AggregateRoot<WorkItemDependencyId>` with `Create(tenantId,dependentId,principalId,type)` → validates `dependent!=principal`, raises `DependencyAddedDomainEvent`)
- [X] T047 [P] [US3] Implement `IDependencyCycleDetector` in `src/Modules/Projects/Projects.Infrastructure/Services/DependencyCycleDetector.cs` (iterative DFS O(V+E) per research Decision 3, filters `RelatedTo` before traversal, `HasCycle(IReadOnlyList<WorkItemDependency> edges, WorkItemDependency candidate)`)
- [X] T048 [P] [US3] Implement `IHierarchyInspector` in `src/Modules/Projects/Projects.Infrastructure/Services/HierarchyInspector.cs` (recursive CTE `WITH RECURSIVE` via `FromSqlRaw` over `projects.work_items` for `GetAncestorIds`/`GetDescendantIds`/`GetRootEpicId`; indexed `(project_id,parent_id)`)
- [X] T049 [US3] Implement `CircularDependencyRule` and `ReparentNoCycleRule` in `src/Modules/Projects/Projects.Domain/Rules/` (CircularDependencyRule ctor `(allEdges,candidate,detector)`, ReparentNoCycleRule ctor `(candidateParentId, hierarchyInspector)`)
- [X] T050 [US3] Implement vertical slices `AddDependency` and `RemoveDependency` in `src/Modules/Projects/Projects.Application/Features/WorkItems/Dependencies/` (`AddDependencyCommand(dependentId,principalId,type,expectedVersion?)` + `Validator` (type in Enumeration, principal != dependent) + `Handler` (intra-project check, load non-RelatedTo edges for project, call detector+`CheckRule(CircularDependencyRule)` inside same `IUnitOfWork` tx repeatable-read, stage `DependencyAddedIntegrationEvent`) + `AddDependencyEndpoint` `POST /api/workitems/{id}/dependencies`; `RemoveDependencyCommand` + `Handler` stages `DependencyRemoved`)
- [X] T051 [US3] Implement `ReparentWorkItem` slice in `src/Modules/Projects/Projects.Application/Features/WorkItems/ReparentWorkItem/` (`ReparentWorkItemCommand(workItemId,newParentId,expectedVersion)` + `Validator` + `Handler` (load workItem, check `Version==expected`, validate targetParent exists in same `ProjectId` or null, `IHierarchyInspector.GetDescendantIds` → reject if `newParentId` is descendant, `IAuthorizationEvaluator` check, `workItem.Reparent(newParentId)` → increments Version + `WorkItemReparented`, stage outbox) + `ReparentWorkItemEndpoint` `POST /api/workitems/{id}/reparent` (only mutation path — no bare ParentId PUT exists))
- [ ] T052 [US3] Implement blocked derivation helper `WorkItemBlockedQueryService` in `src/Modules/Projects/Projects.Domain/Services/` (`IsBlocked(workItemId, dependencies)` — non-RelatedTo unresolved where principal.Status != Completed)

**Checkpoint**: US3 independently testable — 3-node cycle rejected <200 ms, RelatedTo allowed, hierarchy cycle rejected, reparent succeeds and board reflects new epic.

---

## Phase 6: User Story 4 — Assignment with hierarchical authorization (Priority: P2)

**Goal**: Manager assigns work items with checks: assignee active, work item not Completed, and (assignee in assigner's subtree via `IManagementHierarchy`) OR (shared project membership via `IProjectMembership`). Denial is generic + audited; success emits `WorkItemAssigned` + notification event.

**Independent Test**: Seed Root→MgrA→{A1,A2} and MgrB in other branch + project where MgrA,A1 are members but MgrB is not → MgrA→A1 assignment succeeds 200 + notification outbox; MgrB→A1 (no subtree, no shared membership) → 403 generic + `authorization.denied` audit; inactive assignee → 400; Completed item → 400 (spec US4 scenarios 1–5, SC-005).

### Tests for User Story 4 (write FIRST)

- [X] T053 [P] [US4] Unit test `AssignmentPolicyMatrixTests` in `tests/Projects.Tests/Unit/AssignmentPolicyMatrixTests.cs` — matrix (inSubtree=true/false × sharedMembership true/false × isActive true/false × isCompleted true/false) → `IAssignmentPolicy.CanAssign` Result matrix: subtree=true+active+notCompleted→Allow; sharedMembership=true+active+notCompleted→Allow; both false→Forbidden; inactive→Validation regardless of subtree; Completed→Validation
- [X] T054 [P] [US4] Unit test `WorkItemNotCompletedRuleTests` in `tests/Projects.Tests/Unit/WorkItemNotCompletedRuleTests.cs` — `Completed` status → IsBroken=true
- [ ] T055 [P] [US4] Integration test `AssignWorkItemIntegrationTests` in `tests/Projects.Tests/Integration/AssignWorkItemIntegrationTests.cs` — Testcontainers + NSubstitute `IManagementHierarchy` (IsInSubtree) + real `project_members` for `IProjectMembership`: subtree-assign 200 + `WorkItemAssignedIntegrationEvent` + audit, cross-branch without membership 403+audited, shared-membership cross-branch 200, inactive 400, Completed 400, `GetMyTasks` includes assigned item for assignee
- [ ] T056 [P] [US4] Contract test `AssignWorkItemContractTests` in `tests/Projects.Tests/Contract/AssignWorkItemContractTests.cs` — `POST /api/workitems/{id}/assign` per `contracts/workitems-api-contract.md` (200, 400 inactive/completed, 403 generic without leak, 409 stale Version)

### Implementation for User Story 4

- [X] T057 [P] [US4] Implement `IAssignmentPolicy` in `src/Modules/Projects/Projects.Infrastructure/Services/AssignmentPolicy.cs` (per research Decision 4: order `Status!=Completed` → `IUserStateChecker.IsActive` → tenant gate via evaluator → `IManagementHierarchy.IsInSubtree` OR `IProjectMembership.IsMember(assignee,project) && IsMember(assigner,project)`; `IUserStateChecker` is thin interface over identity or stub IsActive=true for now)
- [X] T058 [P] [US4] Implement `IProjectMembership` adapter in `src/Modules/Projects/Projects.Infrastructure/Services/ProjectMembershipService.cs` (queries `projects.project_members` via `ProjectsDbContext` — read-only, fulfills `IsMember(userId,projectId)` and `GetProjectIdsForUser`)
- [X] T059 [US4] Implement `WorkItem.Assign(assigneeId)` domain method in `src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs` (calls `CheckRule(WorkItemNotCompletedRule)`, sets `ResponsibleId=assigneeId`, increments `Version`, raises `WorkItemAssignedDomainEvent`/`WorkItemReassignedDomainEvent`)
- [X] T060 [US4] Implement `AssignWorkItem` vertical slice in `src/Modules/Projects/Projects.Application/Features/WorkItems/AssignWorkItem/` (`AssignWorkItemCommand(workItemId,assigneeId,expectedVersion)` + `Validator` (assigneeId != Guid.Empty, version >0) + `Handler` (load WorkItem, `Version==expected`, `IAuthorizationEvaluator.CanActorPerform(actor,workitem,workitem.assign)` → 403+audit if denied, else `IAssignmentPolicy.CanAssign` → 403+audit or 400 Validation, else `workItem.Assign`, stage `WorkItemAssignedIntegrationEvent` + audit via same tx outbox) + `AssignWorkItemEndpoint` `POST /api/workitems/{id}/assign`)
- [ ] T061 [US4] Implement query slices `GetMyTasks` and `GetTeamTasks` in `src/Modules/Projects/Projects.Application/Features/WorkItems/Queries/` (`GetMyTasksQuery(userId,filters)` → tenant-filtered spec + `AuthorizedWorkItemSpec` + `responsibleId==me`; `GetTeamTasksQuery(managerId)` → `IManagementHierarchy.GetSubtree(managerId)` then `responsibleId IN subtree OR project membership spec`; pagination/sorting per `contracts/kanban-board-contract.md`)
- [ ] T062 [US4] Implement `WorkItemAssignedDomainEvent`/`WorkItemReassignedDomainEvent` and `WorkItemAssignedIntegrationEvent` in `src/Modules/Projects/Projects.Domain/Events/` and `src/Modules/Projects/Projects.Contracts/Events/` (consumed by Notifications SPEC-008)

**Checkpoint**: US4 independently testable — assignment matrix green, subtree vs shared-membership both work, deny audited without leak, `GetMyTasks`/`GetTeamTasks` authorized.

---

## Phase 7: User Story 5 — Kanban projection, filtering, and visualization (Priority: P2)

**Goal**: Kanban board read model: columns by `WorkItemStatus`, swimlanes by assignee/epic, filters/sorting/pagination, progress/criticality/overdue indicators — read-only, never mutates.

**Independent Test**: Seed items across 6 statuses/assignees/epics/dueDates → `GetKanbanBoard(projectId)` groups into correct columns <500 ms; `GET /board?swimlane=assignee` groups by responsibleId with Unassigned lane; `GET /board?swimlane=epic` groups by root Epic; `GET /board?status=Planned&assignee=A1` returns only matching authorized items; `DueDate < today` shows `isOverdue`; board has no mutating endpoint (spec US5 scenarios 1–5, SC-007).

### Tests for User Story 5 (write FIRST)

- [X] T063 [P] [US5] Integration test `KanbanBoardQueryTests` in `tests/Projects.Tests/Integration/KanbanBoardQueryTests.cs` — Testcontainers: seed 50 items across statuses/assignees/epics; `GetKanbanBoard(projectId)` asserts column counts by status, ordered per sort, `isOverdue` true when `DueDate < today && status!=Completed`, `epic` swimlane via `IHierarchyInspector.GetRootEpicId`, `Unassigned` lane present, `tags`/`priority`/`criticality`/`dueRange` filtering, no board endpoint mutates (call `POST /board` → 405)
- [X] T064 [P] [US5] Unit test `KanbanBoardAuthFilteringTests` in `tests/Projects.Tests/Unit/KanbanBoardAuthFilteringTests.cs` — verifies `GetKanbanBoardQueryHandler` composes `TenantSpec` + `AuthorizedWorkItemSpec` (`IManagementHierarchy`+`IProjectMembership`) via `And` before `repository.ListAsync`; cross-branch actor without membership gets 0 items not 403; missing `projectId` → 400
- [ ] T065 [P] [US5] Contract test `KanbanBoardContractTests` in `tests/Projects.Tests/Contract/KanbanBoardContractTests.cs` — `GET /api/projects/{projectId}/board?swimlane&sortBy&page&pageSize` envelope per `contracts/kanban-board-contract.md` (200 with `columns/{status,count,items}`, `swimlanes`, `totalCount`, `overdueCount`, pagination metadata)

### Implementation for User Story 5

- [X] T066 [US5] Implement `AuthorizedWorkItemSpec` and board filter specifications in `src/Modules/Projects/Projects.Infrastructure/Specifications/` (`TenantSpecification`, `ProjectSpecification`, `StatusSpecification`, `AssigneeSpecification`, `EpicAncestorSpecification` via `IHierarchyInspector`, `Priority/CriticalitySpecification`, `TagSpecification`, `DueDateRangeSpecification` — each `: Specification<WorkItem>` composable via `And`)
- [X] T067 [US5] Implement `GetKanbanBoard` query slice in `src/Modules/Projects/Projects.Application/Features/WorkItems/Queries/GetKanbanBoard/` (`GetKanbanBoardQuery(projectId, filters)` + `Validator` (projectId != Guid.Empty, page>=1, pageSize 1..100, swimlane in {assignee,epic,null}) + `Handler` (extract tenant/actor from `TenantContext`, `AllowedUserIds = hierarchy.GetSubtree || projectMembership || explicit grants` via evaluator, compose auth `And` spec before `ListAsync` so SQL WHERE filters, apply filter specs, sort, paginate with `CountAsync`+`ListAsync`, group into `BoardColumn {status,count,items}` + optional `Swimlane {key,columns}`; `isOverdue` computed per row, `blockedDerived` via `WorkItemDependency` lookup) + `GetKanbanBoardEndpoint` `GET /api/projects/{projectId}/board` → `Result.ToHttpResult` per `contracts/kanban-board-contract.md`)
- [X] T068 [US5] Implement `KanbanBoardResponse` DTOs `BoardColumn`, `Swimlane`, `BoardItem` in `src/Modules/Projects/Projects.Contracts/Dtos/KanbanDtos.cs` (BoardItem carries `progress`, `criticality`, `isOverdue`, `blockedDerived`, `epicId`, `version`)
- [X] T069 [US5] Scaffold Web Kanban board component in `src/Web/src/app/features/kanban/kanban-board.component.ts` (Angular + `minimal-ui-design-system` tokens + `ngrx-signal-store` `boardStore` with `withState({columns,swimlanes,filters,sort,page})`, `withMethods({loadBoard, setFilter, dragDrop})` where `dragDrop` dispatches `POST /api/workitems/{id}/status` then `loadBoard` — no board mutation)

**Checkpoint**: US5 independently testable — board query <500 ms on 50 items, filters/swimlanes/overdue correct, no mutating endpoint, auth before fetch.

---

## Phase 8: User Story 6 — Observability: audit, concurrency, and board round-trip (Priority: P3)

**Goal**: Every important change audited (append-only via outbox), concurrent edits conflict with 409 never silent overwrite, and Kanban drag/drop round-trips command→projection (<1 s E2E), with subtree-Spec authorization on all list queries.

**Independent Test**: Two concurrent writes from Version N → one 409; status/assignment change → audit entry + notification integration event durably in same tx; `GET /board → POST /status (valid) → GET /board` shows new column; all board/task list queries are subtree-filtered before fetch (spec US6 scenarios 1–4, SC-004/006/008).

### Tests for User Story 6 (write FIRST)

- [X] T070 [P] [US6] Integration test `ConcurrencyConflictTests` in `tests/Projects.Tests/Integration/ConcurrencyConflictTests.cs` — Testcontainers: two parallel `ChangeWorkItemStatus` from same base Version N (Task.WhenAll) → exactly one 200 (Version N+1) the other 409 `Error.Conflict("Concurrency conflict")` mapped to `Result.ToHttpResult` 409, no silent overwrite, <1 s; also tests `RowVersion` bytea `IsRowVersion()` is present via model inspection
- [ ] T071 [P] [US6] Integration test `OutboxAndAuditTests` in `tests/Projects.Tests/Integration/OutboxAndAuditTests.cs` — assert after any `CreateWorkItem`/`ChangeWorkItemStatus`/`AssignWorkItem`/`ReparentWorkItem`/`AddDependency` commit: `outbox_messages` contains the corresponding `*IntegrationEvent` and, where applicable, `authorization.denied` audit with actor/resourceId/permission/tenant/correlationId in same transaction (poll outbox table)
- [ ] T072 [P] [US6] E2E test `KanbanDragDropRoundTripTests` in `tests/Projects.Tests/E2E/KanbanDragDropRoundTripTests.cs` — `TestHost` against `Api`: `GET /board` → `POST /api/workitems/{id}/status` (InProgress→InReview valid) → `GET /board` asserts item in InReview column, full chain <1 s, board never mutated via query path (POST /board → 405, PUT with status field absent)
- [X] T073 [P] [US6] Unit test `ErrorToHttpMappingTests` in `tests/Projects.Tests/Unit/ErrorToHttpMappingTests.cs` — `Error.Validation→400`, `Error.Forbidden→403 generic (no leak)`, `Error.NotFound→404`, `Error.Conflict→409` via `Result.ToHttpResult`/`GlobalExceptionHandler`

### Implementation for User Story 6

- [ ] T074 [US6] Implement concurrency guard in all mutating handlers (`CreateWorkItem`/`ChangeWorkItemStatus`/`AssignWorkItem`/`ReparentWorkItem`/`AddDependency`/`CompleteWorkItem`) in `src/Modules/Projects/Projects.Application/Features/WorkItems/**/` — each handler asserts `expectedVersion==workItem.Version`, catches `DbUpdateConcurrencyException` from `IUnitOfWork.SaveChangesAsync` → `Error.Conflict("Concurrency conflict")` → 409; `WorkItem.Version` incremented inside aggregate mutation (`Version++`) atomically with `RowVersion`
- [ ] T075 [US6] Implement audit outbox wiring in `src/Modules/Projects/Projects.Infrastructure/Services/AuditOutboxWiring.cs` (or inline in each handler): after `IUnitOfWork.SaveChangesAsync`, verify `IOutboxWriter.StageAsync(new AuditIntegrationEvent { actor, action, resourceType, resourceId, tenantId, correlationId, before/after, version })` is staged in same transaction; handlers are idempotent (retry on `OutboxProcessor` at-least-once requires dedup on `IntegrationEventId` — already via BuildingBlocks)
- [X] T076 [US6] Extend `tests/Architecture/ArchitectureTests.cs` in `tests/Architecture/ArchitectureTests.cs` with Projects boundary guard — asserts `Projects.Domain` has no reference to `Organization.Infrastructure`/`Identity.Infrastructure`, only `Organization.Contracts`; `Projects.Infrastructure` references `Organization.Contracts` only via `IProjectMembership` thin adapter and `IManagementHierarchy` — no direct `OrganizationDbContext` usage; board query handler uses `AuthorizedWorkItemSpec.And` before `ListAsync` (string-scan assertion)
- [ ] T077 [US6] Wire OpenTelemetry + health correlation in `src/Api/Program.cs` (ensure `AddServiceDefaults()` already traces Projects handlers, audit includes `traceId`/`correlationId` via `IHttpContextAccessor`)

**Checkpoint**: US6 independently testable — race → 409, audit+notification durably staged same tx, drag/drop E2E <1 s, architecture guard green.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: API contracts stable, docs updated, performance budgets verified, quickstart validation.

- [ ] T078 [P] Add `Version`/`ETag` concurrency header support in `src/Modules/Projects/Projects.Application/Features/WorkItems/**/` — map `If-Match: W/"<version>"` to `expectedVersion` (fallback to body field) and emit `ETag` in all detail responses per `contracts/workitems-api-contract.md`
- [ ] T079 [P] Add tag normalization deduplication in `src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs` (`SetTags(IReadOnlyCollection<Tag>)` dedupes by `Tag.Value` after trim/lowercase; unit cover in `ValueObjectTests`)
- [ ] T080 [P] Performance smoke in `tests/Projects.Tests/Performance/KanbanBoardPerformanceTests.cs` — seed 50 items, `GET /board` p95 <500 ms, `AddDependency` cycle 100-edge DFS <200 ms (asserts SC-003/SC-007 budgets)
- [ ] T081 Run quickstart validation `specs/003-projects-work-kanban/quickstart.md` end-to-end via `bash -c "$(grep -E 'curl|dotnet test' specs/003-projects-work-kanban/quickstart.md | head)"` or manual curl sequence — all 6 pillars (creation, invalid transition, cycle, concurrency, assignment matrix, audit+E2E) green
- [ ] T082 Update `docs/api/README.md` and `specs/003-projects-work-kanban/contracts/README.md` with contract change log (Projects API 3 endpoints, WorkItems API 8 endpoints, board read model); ensure `IProjectMembership` ADRs recorded as `docs/adr/adr-00x-projects-hierarchy.md` + `adr-00y-enumeration-seeding.md` if not already via 002's `adr-004`
- [X] T083 [P] Code cleanup + `dotnet format OroKanban.slnx` and `dotnet build OroKanban.slnx -warnaserror` final gate — 0 warnings, all tests green (`dotnet test tests/Projects.Tests -v minimal` + `dotnet test tests/Architecture -v minimal`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately.
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories (StronglyTypedIds, Enumerations, ValueObjects, ProjectsDbContext, seeders, domain service contracts must exist before any aggregate/handler).
- **User Stories (Phases 3–8)**: All depend on Foundational (Phase 2).
  - Phases 3 (US1) and 4 (US2) are both P1 — can proceed in parallel once Phase 2 is done (share `WorkItem` aggregate but touch different slices).
  - Phases 5 (US3), 6 (US4), 7 (US5) are P2 — each depends only on Phase 2 + US1 artifacts where noted (hierarchy ParentId, IProjectMembership), not on each other; can run in parallel with P1 stories if staffed.
  - Phase 8 (US6, P3) depends on Phases 3–7 being at least stubbed (needs at least one mutating slice to test concurrency/audit) — schedule after US1+US2.
- **Polish (Phase 9)**: Depends on desired user stories (3–8) complete.

### User Story Dependencies

- **US1 (P1) — Project + WorkItem creation with hierarchy**: No dependencies beyond Foundational — is the MVP data foundation for all other stories.
- **US2 (P1) — Kanban board + validated transitions**: Depends on US1's `WorkItem` aggregate (needs items to transition) but can start in parallel once `WorkItem` aggregate skeleton exists; must integrate with US1's `CreateWorkItem` for E2E.
- **US3 (P2) — Hierarchy reparenting + dependency cycles**: Depends on US1's hierarchy (`ParentId`) plus `WorkItemDependency` is new — integrate with US1's creation slice but independently testable via its own chain tests.
- **US4 (P2) — Assignment with hierarchical auth**: Depends on US1's `ProjectMember` → `IProjectMembership` plus `Organization.Contracts/IManagementHierarchy` from 002 — needs US1 for project membership; independent via NSubstitute hierarchy stub.
- **US5 (P2) — Kanban projection/filtering/visualization**: Depends on US1+US2 items to project but read model is independent — needs `AuthorizedWorkItemSpec` from US4 conceptually but can start with auth stub.
- **US6 (P3) — Audit, concurrency, round-trip**: Depends on all prior stories having at least one mutating slice; P3 validation only.

### Within Each User Story

1. Tests (unit + integration + contract) MUST be written and FAIL before implementation (TDD, Constitution XXI).
2. Domain models/aggregates/ValueObjects → domain services/rules → handlers/validators → endpoints.
3. `Specification<T>` before `ListAsync` (auth before fetch — never post-filter).
4. `IOutboxWriter.StageAsync` + `IUnitOfWork.SaveChangesAsync` in same tx for every write.
5. Story checkpoint must show independent green tests before next story begins.

### Parallel Opportunities

- Phase 1: T003, T004, T005 can run in parallel.
- Phase 2: T007, T008, T009, T010, T013 can run in parallel (different files); T006 blocks T009's StronglyTypedId usage but only briefly.
- Phases 3–7: Once Phase 2 completes, US1–US5 can be staffed in parallel (different features/directories).
- Within each story: All tests T016–T021 (US1), T030–T034 (US2), etc. can run in parallel (different files); implementation tasks T022–T024 (US1 models) can run in parallel.
- Example per-story parallelism is shown below.

---

## Parallel Example: User Story 1 (Phase 3)

```bash
# Launch all US1 tests in parallel (different files, no dependencies):
Task: "Unit test ProjectAggregateTests in tests/Projects.Tests/Unit/ProjectAggregateTests.cs"        # T016
Task: "Unit test WorkItemTypeEnumerationTests in tests/Projects.Tests/Unit/WorkItemTypeEnumerationTests.cs"  # T017
Task: "Unit test ValueObjectTests in tests/Projects.Tests/Unit/ValueObjectTests.cs"                 # T018
Task: "Integration test ProjectPersistenceTests in tests/Projects.Tests/Integration/ProjectPersistenceTests.cs"   # T019
Task: "Integration test WorkItemHierarchyPersistenceTests in tests/Projects.Tests/Integration/WorkItemHierarchyPersistenceTests.cs" # T020
Task: "Contract test ProjectsApiContractTests in tests/Projects.Tests/Contract/ProjectsApiContractTests.cs"    # T021

# Launch all US1 models in parallel (after tests fail):
Task: "Project aggregate in src/Modules/Projects/Projects.Domain/Aggregates/Project.cs"              # T022
Task: "ProjectMember and Milestone entities in src/Modules/Projects/Projects.Domain/Entities/"        # T023
Task: "WorkItem aggregate create path in src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs" # T024
```

## Parallel Example: User Story 3 (Phase 5 — cycle detection)

```bash
Task: "DependencyCycleDetectorTests in tests/Projects.Tests/Unit/DependencyCycleDetectorTests.cs"  # T041
Task: "ReparentNoCycleRuleTests in tests/Projects.Tests/Unit/ReparentNoCycleRuleTests.cs"         # T042
Task: "WorkItemDependencyAggregateTests in tests/Projects.Tests/Unit/WorkItemDependencyAggregateTests.cs" # T043
# Then parallel implementation:
Task: "DependencyCycleDetector in src/Modules/Projects/Projects.Infrastructure/Services/DependencyCycleDetector.cs" # T047
Task: "HierarchyInspector CTE in src/Modules/Projects/Projects.Infrastructure/Services/HierarchyInspector.cs"       # T048
```

---

## Implementation Strategy

### MVP First (US1 only — minimum viable increment)

1. Complete Phase 1 Setup (T001–T005) → `dotnet build` green.
2. Complete Phase 2 Foundational (T006–T015) → `projects` schema + enumeration seeds.
3. Complete Phase 3 US1 (T016–T029) → Project + hierarchical WorkItem creation with outbox.
4. **STOP and VALIDATE**: `dotnet test --filter US1` green + manual `POST /api/projects` + `POST /api/projects/{id}/workitems` hierarchy E2E via `quickstart.md` Setup section.
5. Deploy/demo if ready — US1 alone delivers organizational taxonomy + traceable work items, feeds Golden Rule A membership.

### Full P1 (US1+US2 — Kanban experience MVP)

1. Above, then add Phase 4 US2 (T030–T040) — validated transitions + status commands.
2. Validate: board groups by status, invalid `Backlog→Completed` rejected 400 and board unchanged, exhaustive 36-pair suite green.
3. This is the recommended MVP for external demo — projects + work items + Kanban columns with controlled transitions.

### Incremental Delivery (P2, P3)

1. Add Phase 5 US3 → reparent + dependency cycle prevention (<200 ms).
2. Add Phase 6 US4 → hierarchical assignment matrix + `GetMyTasks`/`GetTeamTasks` with subtree+project-membership auth.
3. Add Phase 7 US5 → board filters/swimlanes/sorting/pagination/overdue — performance <500 ms on 50 items.
4. Add Phase 8 US6 → concurrency 409 never silent overwrite, same-tx audit+notification outbox, drag/drop E2E <1 s.
5. Each increment is independently testable and deployable without breaking prior stories (branch by contract, `Result`/`Error` envelope stable).

### Parallel Team Strategy

With 3 developers after Phase 2:

- Dev A: US1 (Phase 3) + US2 (Phase 4) — owns `Project`/`WorkItem` aggregates + transitions.
- Dev B: US3 (Phase 5) — owns `WorkItemDependency` + `IDependencyCycleDetector` + `IHierarchyInspector` + reparent.
- Dev C: US4 (Phase 6) + US5 (Phase 7) — owns `IAssignmentPolicy`/`IProjectMembership` + `GetKanbanBoard` auth-composed read model + Web `kanban-board.component.ts`.

US6 (Phase 8) is the integration gate — team syncs to wire audit/concurrency/E2E across all slices.

---

## Notes

- **Constitution traceability**: VI (domain rules T035–T037, T047–T049, T057), VII (subtree+project-membership via `IManagementHierarchy`/`IProjectMembership` T057–T058, T066–T067), VIII (append-only audit via same-tx outbox T072, T075), XIV (status map + `TransitionIsAllowedRule` T030–T037, board never mutates T067), XVI (stable contracts T025–T029, T038, T050–T051, T060, T067 — DTOs in `Projects.Contracts`, `Result→HTTP` 400/403/409), XII–XIII (ProgressValue/Effort VOs T008, T018), XX–XXI (TDD phase ordering, `AuthorizationSpec.And` before `ListAsync` T064, T066).
- `[P]` = different files, no dependency on incomplete tasks — can be parallelized.
- `[US#]` maps task to specific user story for traceability (setup/foundational/polish have no story label per required format).
- File paths are absolute-from-repo-root (e.g., `src/Modules/Projects/Projects.Domain/Aggregates/WorkItem.cs`).
- FR-010: any new file/project via platform CLIs (`dotnet new classlib` style) — not manual copy — where applicable.
- Avoid vague tasks, same-file conflicts, or cross-story dependencies that break independent testability.
