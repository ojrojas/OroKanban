# OroKanban — Refined Specifications (SDD)

**Version**: 1.0.0 | **Date**: 2026-08-31
**Status**: Refined specification baseline for Spec-Driven Development

**Authoritative references** (read before implementing any spec):

- `.specify/memory/constitution.md` — v1.2.0, 22 principles (I–XXII)
- `draft/libraries/buildingblocks.md` — BuildingBlocks canon (DDD + Vertical Slice + CQRS + EventBus)
- `draft/oroidentityserver-specification.md` — OroIdentityServer integration canon
- `.agents/skills/ddd-project-planner` — mandatory modeling methodology
- `.agents/skills/dotnet-ai` (technology-selection) — mandatory AI/ML decision tree
- `.agents/skills/minimal-ui-design-system` — mandatory UI design system
- `.agents/skills/ngrx-signal-store` — mandatory frontend state management

Every spec below is written in English, numbered `000`–`014`, and supersedes the earlier
draft specs in the same order. Do not implement `spec/NNN-*.md` files without this
refinement as the source of truth.

---

# Part 0 — Global Foundations

Applies to every spec. Individual specs reference these sections instead of restating them.

## 0.1 Two Golden Rules

### Golden Rule A — The Authorization Stack

The core of this system is NOT `User → Role`. Authorization is the composition:

```
Identity  +  Role/Permission  +  Organization  +  Management Hierarchy
         +  Project Membership  +  Resource Ownership  +  Document Classification
```

This is what allows Manager A to administer their subtree recursively while Manager B
cannot accidentally query tasks or documents from Manager A's branch, even when both hold
the same technical role. Every query, search result, dashboard, and AI retrieval MUST be
filtered through this stack. RBAC is a subset, never the whole model.

### Golden Rule B — Authorize Before Retrieval (never after)

For documents and LLM/RAG pipelines, the ONLY valid flow is:

```
User → Authorization → Documents the user can actually read
     → Authorized indexing → Authorized retrieval → LLM → Answer + Sources
```

The forbidden anti-pattern is: `All documents → global index → LLM → filter the answer`.
That opens an information-leak channel between organizational branches. The retrieval
layer MUST enforce document authorization BEFORE content reaches the LLM, and the answer
MUST carry its sources so the provenance is auditable.

## 0.2 Domain Classification

| Classification | Bounded Contexts | Rationale |
|---|---|---|
| **Core** | Projects & Work Management; Metrics & Progress; Documents; AI Processing (LLM) | Competitive advantage: hierarchical management, explainable progress, traceable AI over classified documents. Deserve the deepest modeling and highest test investment. |
| **Supporting** | Organization & Hierarchy; Planning; Search/Indexing; Notifications; Audit | Necessary to deliver the core and to satisfy accountability, but not differentiating. Model properly, reuse where possible. |
| **Generic** | Identity & Access (consumed from OroIdentityServer); Persistence; Observability; Deployment | Solved problems. Consume `oroidentityserver` as an external system; use standard .NET/BuildingBlocks machinery. Do not over-model. |

## 0.3 Bounded Contexts & Context Map

| # | Bounded Context | Classification | Responsibility |
|---|---|---|---|
| BC-01 | Identity & Access (app-owned authorization policies) | Generic (consumed) | OIDC/OAuth2 consumption, JWT validation, claims transformation, permission catalog |
| BC-02 | Organization & Management Hierarchy | Supporting | Org units, management relationships, recursive subtree evaluation |
| BC-03 | Projects & Work Management (incl. Kanban) | Core | Projects, work items, subtasks, dependencies, state machines, assignments |
| BC-04 | Metrics & Progress (incl. Planning) | Core | Configurable metrics, explainable progress calculation, deadlines, milestones |
| BC-05 | Documents | Core | Document lifecycle, classification, versioning, metadata, access control |
| BC-06 | AI Processing (LLM) | Core | Provider-agnostic AI ops, prompt versioning, provenance, human review, authorized RAG |
| BC-07 | Search & Indexing | Supporting | Authorization-filtered search across projects, work items, documents, content |
| BC-08 | Audit | Supporting | Append-only audit trail, audit search |
| BC-09 | Notifications | Supporting | Event-driven notifications, preferences, channels |
| BC-10 | Platform (Foundation) | Generic | Aspire orchestration, ServiceDefaults, persistence conventions, telemetry |

**Context Map** (relationship types):

```
oroidentityserver ──(OpenID Connect / OAuth2, upstream identity source)──► BC-01
BC-01 ──(Customer/Supplier: supplies identity, roles, tenant_id claims)──► all contexts
BC-02 ──(Shared Kernel: hierarchy & subtree evaluation)──► BC-03, BC-04, BC-05, BC-06, BC-07, BC-08, BC-09
BC-03 ──(Customer/Supplier: work item & project facts)──► BC-04, BC-05, BC-06, BC-07, BC-08, BC-09
BC-05 ──(Customer/Supplier: document facts & classifications)──► BC-06, BC-07, BC-08
BC-06 ──(Customer/Supplier: AI results & provenance)──► BC-03, BC-04, BC-07, BC-08
BC-07 ──(ACL: authorized index access)──► all querying contexts
BC-08 ──(Customer/Supplier: audit events)──◄── all contexts (via outbox + integration events)
```

Rules:

- Cross-context communication is via **integration events** (BuildingBlocks EventBus + transactional outbox) or explicit application contracts. Never via another context's DbContext.
- BC-02's hierarchy/subtree evaluation is the platform's **Shared Kernel** — the one capability every other context consumes (through a published contract, not shared entities).
- The boundary toward `oroidentityserver` is an **Anti-Corruption Layer**: OIDC claims are translated into application identity contracts; OroKanban never re-implements identity.

## 0.4 Ubiquitous Language

| Term | Meaning |
|---|---|
| **Root Manager** | Top of a management hierarchy (chief). |
| **Manager** | A user who manages other users; may manage other managers. |
| **Subordinate** | A user managed (directly or transitively) by a manager. |
| **Subtree** | The set of users an actor can reach by following management relationships downward. |
| **Branch** | One manager's exclusive slice of the hierarchy; cross-branch access requires explicit grant or project membership. |
| **Work Item** | Any unit of tracked work (epic, feature, task, subtask) — one aggregate, typed by `WorkItemType`. |
| **State Transition** | An authorized, audited, validated movement of a work item between statuses. |
| **Progress Explanation** | The persisted calculation trace that justifies a progress value. |
| **Milestone** | A dated, verifiable checkpoint within a project. |
| **Document** | A first-class domain object with identity, classification, and lifecycle — never "a file attached to a task". |
| **Document Version** | An immutable, published snapshot of a document's content. |
| **Classification** | The security classification of a document (extensible per organization). |
| **Provenance** | The full record of where an AI result came from: source, version, operation, model, prompt version, actor. |
| **Review** | The human approval step that gates non-authoritative AI results (Generated → Pending Review → Approved/Rejected). |
| **Authorized Retrieval** | Retrieval that filters by the full authorization stack (Golden Rule B) before content reaches a model. |
| **Discovery Document** | The catalog produced by SPEC-000 before any production feature. |

## 0.5 BuildingBlocks Canon Mapping

All code (human or AI-generated) MUST use these primitives — no MediatR, no MassTransit,
no AutoMapper.

| Concern | BuildingBlocks primitive | Used by |
|---|---|---|
| Identity of aggregates | `StronglyTypedId<T>` | all contexts |
| Aggregate consistency | `AggregateRoot<TId>`, `CheckRule(IBusinessRule)` | all contexts |
| Value semantics | `ValueObject` | all contexts |
| Enumerations with behavior (statuses, types) | `Enumeration` | BC-03, BC-04, BC-05, BC-06 |
| Domain events | `IDomainEvent`, `RaiseDomainEvent` (dispatched in `SaveChanges`) | all contexts |
| Domain queries | `Specification<T>` (+ `And`/`Or`/`Not`, paging, `IsSatisfiedBy` for tests) | all contexts |
| Commands/queries | `ICommand`/`IQuery` + handlers via own `ISender` | all contexts |
| Pipeline | `LoggingBehavior`, `ValidationBehavior` | all contexts |
| HTTP surface | `IEndpoint` (vertical slices), `Result → HTTP`, `GlobalExceptionHandler` | all API modules |
| Persistence | `AppDbContextBase`, `EfRepository` + `SpecificationEvaluator` | all contexts |
| Cross-context messaging | `IntegrationEvent`, `IEventBus`, RabbitMQ topic exchange, publisher confirms, manual ack, exponential retry, **at-least-once** (handlers MUST be idempotent) | BC-02…BC-09 |
| Reliability | Transactional outbox (`IOutboxWriter` + `OutboxProcessor`) | all contexts emitting integration events |
| Observability | ServiceDefaults (OpenTelemetry OTLP, `/health`, `/alive`, HTTP resilience) | all services |
| Logging | Serilog structured logging | all services |

Canonical vertical slice layout (per feature):

```
Features/<Feature>/<Action>.cs
  ├── record <Action>Command(...) : ICommand<Result<...>>
  ├── class <Action>Validator : Validator<...>          // runs in ValidationBehavior
  ├── class <Action>Handler : ICommandHandler<...>      // manual mapping, CheckRule, outbox
  └── class <Action>Endpoint : IEndpoint                // ISender + result.ToXxxResult()
```

## 0.6 Skill Mandates

| Spec | Mandatory skill / reference |
|---|---|
| All modeling | `.agents/skills/ddd-project-planner` (aggregates, events, Given/When/Then, ADRs, traceability) |
| SPEC-006 | `.agents/skills/dotnet-ai` (technology-selection decision tree) |
| SPEC-009 | `.agents/skills/minimal-ui-design-system` + `.agents/skills/ngrx-signal-store` |
| All backend code | `draft/libraries/buildingblocks.md` |
| All identity code | `draft/oroidentityserver-specification.md` |

## 0.7 Shared Acceptance-Criteria Grammar

All acceptance criteria in the specs below use Given/When/Then. Additionally, every spec
inherits these global criteria:

- Given any protected resource, When a query executes, Then the authorization stack (Golden Rule A) is applied before results are returned.
- Given any cross-context event, When it is delivered, Then the handler is idempotent (at-least-once delivery).
- Given any state-changing action, When it succeeds, Then an audit event is emitted via the outbox.
- Given any expected failure, When the domain rejects it, Then a `Result`/`Error` is returned — not an exception.

---

# SPEC-000 — Repository Discovery

**Bounded Context**: BC-10 Platform (Generic) · **Depends on**: nothing · **Blocks**: all other specs

## Objective

Convert the constitution's Repository Discovery Gate (Principle I, XIII of §Development
Lifecycle) into an executable first deliverable. No production feature starts before the
discovery document exists. This spec exists so the constitution becomes a concrete
architecture instead of guesses about what already exists.

## Requirements

**R1 — Catalog draft/\***: Enumerate and summarize every document under `draft/`,
specifically `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md`,
listing the primitives, endpoints, flows, and configuration knobs each provides.

**R2 — Catalog skills**: Enumerate `.agents/skills/` and record, per skill, the mandate it
imposes (per constitution Principle XXI/XXII).

**R3 — Catalog solution**: Inspect `OroKanban.slnx`, `src/BuildingBlocks/*`, project files,
`Directory.Build.props`, `Directory.Packages.props`, `global.json`; record target frameworks,
central package management versions, and existing code.

**R4 — Catalog orchestration & infrastructure**: Inspect `OroKanban.AppHost/AppHost.cs`,
`aspire.config.json`; record declared resources, external references, connection strings.

**R5 — Catalog cross-cutting state**: Record what exists for identity integration,
persistence, UI, testing, and CI/CD — including explicit "not present" findings.

**R6 — Produce the Discovery Document**: Write findings to
`draft/discovery/000-repository-catalog.md` (or the repo's established location), including:
capability matrix (needed vs. provided by draft/* vs. gap), and the ADR queue (see §ADR
checklist at the end of this file).

## Acceptance Criteria

- Given the repository, When discovery completes, Then every `draft/*` document is cataloged with its reusable capabilities.
- Given a proposed dependency, When R2's three questions are asked (draft/* provides it? skill prefers an approach? existing NuGet covers it?), Then only a negative answer on all three permits proposing a new dependency.
- Given the discovery document, When any later spec starts, Then it cites the discovery entries it relies on.
- Given a gap between constitution requirements and repository reality, When discovered, Then it is recorded as an ADR candidate, not silently improvised.

## TDD Strategy

Discovery is documentary; tests are not applicable. The "test" is that SPEC-001's
architecture tests can be written directly from the discovery document.

---

# SPEC-001 — Foundation and Architecture

**Bounded Context**: BC-10 Platform (Generic) · **Depends on**: SPEC-000

## Objective

Establish the .NET 10 + Aspire technical foundation: solution structure, module
skeletons, persistence conventions, and the external identity integration — all conforming
to BuildingBlocks canon.

## Requirements

**R1 — Solution structure**: Modules are bounded contexts (Constitution Principle V),
physically organized as (adapt to discovery findings):

```
src/
  BuildingBlocks/            # existing canon, untouched
  OroKanban.AppHost/         # Aspire orchestration
  OroKanban.ServiceDefaults/ # host defaults
  Modules/
    Identity/                # BC-01: authorization policies (identity itself is external)
    Organization/            # BC-02
    Projects/                # BC-03 (incl. WorkManagement)
    Metrics/                 # BC-04
    Documents/               # BC-05
    AiProcessing/            # BC-06
    Search/                  # BC-07
    Audit/                   # BC-08
    Notifications/           # BC-09
  Api/                       # composition host exposing endpoints (or per-module hosts per ADR-001)
  Web/                       # frontend Angular(latest)
tests/
  Unit/ Integration/ Architecture/ EndToEnd/
```

**R2 — Module skeleton**: Each module follows DDD layering per BuildingBlocks:
`Domain/` (aggregates, VOs, specifications, domain events, rules),
`Application/` (vertical slices: command/query/handler/endpoint),
`Infrastructure/` (EF DbContext inheriting `AppDbContextBase`, outbox, repositories),
`Contracts/` (integration events, public DTOs). No module references another module's
Infrastructure or Domain project.

**R3 — Persistence convention**: Every module DbContext inherits `AppDbContextBase`,
applies `OutboxEntityTypeConfiguration`, uses Npgsql, and supports optimistic concurrency
via row versions on mutable aggregates.

**R4 — Aspire AppHost**: The AppHost composes modules and declares external dependencies
(PostgreSQL, RabbitMQ, Redis) plus the external `oroidentityserver` container resource.
The AppHost MUST NOT duplicate identity functionality (Constitution Principle IV).

**R5 — External identity integration**: OroKanban consumes `oroidentityserver` via its
OIDC discovery endpoint (`GET /.well-known/openid-configuration`). Client registration
happens in OroIdentityServer (`POST /api/applications` or admin UI) with
`authorization_code` + `refresh_token` grants; the app receives Authority/ClientId/Secret
through environment-specific configuration only. Multi-tenancy maps to the `tenant_id`
claim published by `/connect/userinfo`.

**R6 — ServiceDefaults**: All services use `AddServiceDefaults()` (OTel OTLP,
`/health`, `/alive`, resilient HTTP). Logging via Serilog (BuildingBlocks.Logger).

**R7 — Architecture tests**: An `Architecture/` test project enforces, at minimum:
BuildingBlocks-only dispatch (no MediatR/MassTransit/AutoMapper references), module
boundary rules (no cross-module Infrastructure references), and DbContext inheritance rules.

## Domain Model

Platform context holds no business aggregates. Its "model" is the composition itself.

## Application Layer (representative)

- `SeedDevelopmentData` (dev-only command) — bootstrap an org, users, roles via OroIdentityServer admin APIs.
- `GetPlatformHealth` (query) — composed health of modules + external identity reachability.

## Acceptance Criteria

- Given the solution, When built, Then it compiles on .NET 10 with zero warnings for analyzer-enabled projects.
- Given `aspire start`, When the AppHost runs, Then all module services and infra containers are discoverable in the dashboard.
- Given the OIDC configuration, When a module starts, Then it validates tokens against the external `oroidentityserver` discovery document.
- Given any module, When a dependency analysis runs, Then no prohibited dependency (MediatR, MassTransit, AutoMapper, cross-module internals) exists — verified by architecture tests.
- Given any environment, When identity settings are absent, Then startup fails fast with a clear error rather than defaulting silently.

## TDD Strategy

- **Unit**: configuration binding tests (identity section fails closed).
- **Integration**: AppHost smoke test — modules start, health endpoints respond, identity discovery is reachable.
- **Architecture**: the R7 test suite as the continuous guard.

**Constitution traceability**: Principles I, III, IV, XXI; §Configuration; §Aspire Requirements.

---

# SPEC-002 — Identity, Access and Organization

**Bounded Contexts**: BC-01 Identity & Access + BC-02 Organization (Supporting) · **Depends on**: SPEC-001

## Objective

Implement authentication (consumed) and hierarchical authorization (app-owned), including
the recursive management hierarchy that makes Golden Rule A enforceable.

## Requirements

**R1 — Identity consumed, not owned**: Authentication is delegated entirely to
`oroidentityserver` (authorization-code + refresh flows; JWT validation via discovery).
OroKanban stores no passwords, no login UI, no token issuance. Claims consumed:
`sub`, email, name, roles, `tenant_id`.

**R2 — App-owned authorization model**: The platform owns permission evaluation.
Permission catalog (extensible, seeded, not hard-coded): `project.read/create/update/delete`,
`workitem.read/create/assign/update/complete`, `document.read/upload/classify/version/approve`,
`ai.execute/review/approve`, `audit.read`, `organization.manage`, plus a
permission-to-role seed for the initial role catalog: `RootManager`, `Manager`,
`Supervisor`, `Contributor`, `Reviewer`, `Auditor`, `DocumentManager`, `ProjectManager`,
`AIReviewer`, `Administrator`. Roles live in OroIdentityServer; role→permission mapping and
all evaluation logic live in OroKanban.

**R3 — Management hierarchy**: `ManagementRelationship` is the app-owned aggregate:
`ManagerId → SubordinateId`, `Type (Manager|Supervisor|Contributor)`, effective dates,
`OrganizationUnit` scope. Arbitrary depth; a manager may manage other managers. Cycle
prevention is a domain rule (`CheckRule`) — a user can never become their own ancestor.

**R4 — Subtree evaluation (Shared Kernel)**: BC-02 publishes a contract
`IManagementHierarchy` exposing: `IsInSubtree(managerId, userId)`,
`GetSubtree(managerId)`, `GetAncestors(userId)`, `GetCommonAncestor(a, b)`.
Implementation strategy (recursive CTE vs. closure table vs. ltree) is an ADR decision
informed by SPEC-000/010. Results are cacheable (Redis) with explicit invalidation on
hierarchy-change integration events.

**R5 — Authorization evaluator**: A single domain service composes Golden Rule A:
`Identity + Role/Permission + Organization (tenant) + Subtree + Project Membership +
Ownership + Classification → Decision`. Policy inputs are explicit; failures are
distinguishable (deny reasons are logged/audited but never leaked to callers).

**R6 — Cross-branch isolation**: A subordinate SHALL NOT automatically access resources
in another branch. Cross-branch access requires explicit grant or project membership.
Every list/search/dashboard query composes a subtree `Specification<T>` with the resource query.

**R7 — Authorization failures are audited**: Deny decisions emit audit events via the outbox.

## Domain Model

**Aggregates**

- `ManagementRelationship` (root: `ManagementRelationshipId` — `StronglyTypedId`)
  - Invariants: no cycles (`SubtreeCannotContainManagerRule`), manager ≠ subordinate,
    single active manager per subordinate per unit (configurable), org-scope consistency.
  - Events: `ManagerAssignedToUser`, `ManagerRemovedFromUser`, `OrganizationUnitRestructured`.
- `OrganizationUnit` (root: `OrganizationUnitId`) — tree of org units within a tenant.
  - Events: `OrganizationUnitCreated`, `OrganizationUnitMoved`.
- `ExplicitGrant` (root: `ExplicitGrantId`) — cross-branch exception: user × resource × permission × expiresAt.
  - Events: `GrantIssued`, `GrantRevoked`.

**Value Objects**: `HierarchyPath`, `SubtreeScope`, `PermissionCode`, `GrantScope`.

**Domain services**: `IAuthorizationEvaluator` (R5), `IManagementHierarchy` (R4).

## Application Layer (representative)

- `AssignManager(command)` — creates relationship, raises events, invalidates subtree cache.
- `MoveOrganizationUnit(command)`, `IssueExplicitGrant(command)`, `RevokeExplicitGrant(command)`.
- `GetSubtree(query)`, `WhoReportsToMe(query)`, `CanActorPerform(query)` (policy probe used by tests and UI).

## Acceptance Criteria

- Given a valid OIDC flow, When a user signs in, Then claims (`sub`, roles, `tenant_id`) are available and no local login exists.
- Given Manager A with subtree {A1, A2, M-A1, M-A1's reports}, When A queries tasks, Then only subtree + explicit-grant + project-member resources return.
- Given Manager B in a different branch, When B queries A's subtree resources, Then results are absent (not error-leaking) unless a grant or project membership exists.
- Given a proposed relationship X→Y where Y is an ancestor of X, When `AssignManager` executes, Then the domain rejects it with `Error` and no state changes.
- Given any deny decision, When evaluation completes, Then an audit event with actor, resource, permission, and correlation ID is emitted.
- Given a hierarchy change, When the integration event is consumed, Then cached subtree results are invalidated.

## TDD Strategy

- **Unit**: cycle rules, grant expiry, evaluator composition (all combinations of Golden Rule A inputs), `IsSatisfiedBy`-style specification tests.
- **Integration**: hierarchy storage strategy (from ADR), Redis invalidation, OIDC claim mapping against a containerized OroIdentityServer.
- **Security matrix rows** (per SPEC-013): Owner / Manager / Manager's Manager / Peer / Different Branch / Auditor / Admin / Anonymous — expected results defined explicitly for `ManagementRelationship` and `ExplicitGrant`.

**Constitution traceability**: Principles II, VI, VII, VIII, XV, XIX; Golden Rule A.

---

# SPEC-003 — Projects, Work Items and Kanban

**Bounded Context**: BC-03 Projects & Work Management (Core) · **Depends on**: SPEC-002

## Objective

Implement the core project and Kanban experience over hierarchical, stateful work items
with validated transitions and dependencies.

## Requirements

**R1 — Project aggregate**: `Project` owns identity, owner, manager, participants
(`ProjectMember` with roles), dates, status, priority, criticality, metrics wiring
(SPEC-004), milestones, work items, documents. Project membership feeds Golden Rule A.

**R2 — Work item aggregate**: Single `WorkItem` aggregate typed by `WorkItemType`
(taxonomy configurable — e.g., Epic/Feature/Task/Subtask — as `Enumeration` values, not
hard-coded classes). Fields per the constitution §5: identity, parent, title, description,
type, status, priority, criticality, owner/responsible/reviewer, dates, progress,
estimated/actual effort, tags, version (optimistic concurrency).

**R3 — Hierarchy**: `ParentId` supports `Epic → Feature → Task → Subtask` to arbitrary
depth via the same aggregate type; reparenting is a validated command (not a bare update).

**R4 — State machine**: `WorkItemStatus` is an `Enumeration` with an allowed-transition
map: `Backlog → Planned → In Progress → Blocked ↔ In Review → Completed` (plus reopen rules).
Transitions are domain operations: `CheckRule(new TransitionIsAllowedRule(current, target))`,
authorized (SPEC-002 evaluator), audited, and raise `WorkItemStatusChanged`. The UI can
never set status directly — drag/drop issues a `ChangeWorkItemStatus` command.

**R5 — Dependencies**: `WorkItemDependency` (dependent, principal, type:
`Blocks/BlockedBy/DependsOn/RelatedTo`). Adding a dependency runs cycle detection as a
domain service; `CircularDependencyRule` rejects creation. `Blocked` status derives
partially from unresolved `BlockedBy`/`DependsOn` links.

**R6 — Assignment**: `AssignWorkItem` is a command validating: assignee is in the
assigner's subtree or the same project, assignee is active, work item is not completed.
Raises `WorkItemAssigned` (→ SPEC-008 notification + audit).

**R7 — Kanban projection**: Board state is a read model (query side) composed from work
items; columns by status, swimlanes by assignee/epic, filters/sorting, progress and
criticality visualization, overdue indicators. The board NEVER mutates state directly.

## Domain Model

**Aggregates**

- `Project` (root: `ProjectId`) — Events: `ProjectCreated`, `ProjectMemberAdded/Removed`,
  `ProjectStatusChanged`, `MilestoneReached`.
- `WorkItem` (root: `WorkItemId`) — Events: `WorkItemCreated`, `WorkItemStatusChanged`,
  `WorkItemAssigned`, `WorkItemReassigned`, `WorkItemReparented`, `WorkItemCompleted`,
  `ProgressRecalculated`, `WorkItemBlocked`, `DependencyAdded`.
- `WorkItemDependency` (root: `WorkItemDependencyId`) — Event: `DependencyRemoved`.

**Value Objects**: `WorkItemStatus` (Enumeration + transition map), `WorkItemPriority`,
`Criticality`, `Effort`, `Tag`, `DueDate`, `ProgressValue`.

**Domain services**: `IDependencyCycleDetector`, `IAssignmentPolicy` (who can assign whom —
uses `IManagementHierarchy`), `IWorkItemTransitionPolicy`.

## Application Layer (representative)

- `CreateProject`, `AddProjectMember`, `CreateWorkItem`, `ReparentWorkItem`,
  `ChangeWorkItemStatus`, `AssignWorkItem`, `AddDependency`, `RemoveDependency`,
  `CompleteWorkItem`.
- Queries: `GetKanbanBoard(projectId, filters)`, `GetWorkItemDetail(id)`, `GetMyTasks`,
  `GetTeamTasks(managerId)` (subtree-filtered).

## Acceptance Criteria

- Given a project, When a manager within scope creates a work item, Then it appears with version 1 and a `WorkItemCreated` event in the outbox.
- Given a work item in `Backlog`, When a drag/drop issues `ChangeWorkItemStatus` to `Completed`, Then the domain rejects the invalid transition and the board re-renders unchanged.
- Given a dependency chain A→B→C, When adding C→A, Then `CircularDependencyRule` rejects it.
- Given two concurrent updates to one work item, When both save, Then the stale one receives a concurrency `Error` (optimistic version), never a silent overwrite.
- Given a subtree member assignment request from outside the subtree without project membership, When `AssignWorkItem` executes, Then it is denied and audited.
- Given any status/assignment change, When committed, Then an audit event and (where relevant) a notification integration event are emitted.

## TDD Strategy

- **Unit**: transition map exhaustively (every from→to pair), cycle detection, assignment
  policy (subtree/membership matrix), reparenting rules, concurrency version checks.
- **Integration**: EF persistence of the hierarchy, board query performance, outbox events.
- **E2E**: Kanban drag/drop → command → projection round trip; the full chain from SPEC-013.

**Constitution traceability**: Principles VI, VIII, XIV, XVI; §Work Item Model.

---

# SPEC-004 — Metrics, Progress and Planning

**Bounded Context**: BC-04 Metrics & Progress (Core) · **Depends on**: SPEC-003

## Objective

Make progress measurable, configurable, deterministic, and explainable — never an
arbitrary number (Constitution Principle XII).

## Requirements

**R1 — Configurable metric definitions**: `MetricDefinition` per project/template:
code, name, dimension (completion %, deadline adherence, content completeness, quality,
risk, criticality, effort, dependency health, document compliance, review status),
weight, target, threshold, evidence requirement. Definitions are version-aware.

**R2 — Progress strategy**: Progress is computed by a pluggable strategy:
`Progress = Σ(componentProgress × componentWeight) / Σ(componentWeight)` where components
include completed/weighted subtasks, deliverables, milestones hit, validation criteria,
approved evidence, and (optionally) manually reported values. Strategy selection is
configurable per project. Manual override is itself an audited, permissioned command.

**R3 — Explainability**: Every computed value persists a `ProgressExplanation`:
components used, their weights and values, strategy ID, inputs at calculation time
(subtask snapshot, evidence list). The value must be reconstructible.

**R4 — Deadline semantics**: The system derives `OnTime | AtRisk | Overdue |
CompletedLate | CompletedOnTime` from dates + status. These are VOs, not UI strings.

**R5 — Planning**: `Milestone` (dated, verifiable, linked to work items) with explicit
`MilestoneReached` criteria evaluation. Plans are version-aware.

**R6 — Manager dashboards**: Read models for: totals, active, overdue, blocked, tasks by
manager/subordinate, completion %, critical, upcoming deadlines, project health, metric
violations — all subtree-filtered via Golden Rule A (uses `IManagementHierarchy`).

## Domain Model

**Aggregates**

- `MetricDefinition` (root: `MetricDefinitionId`) — Events: `MetricDefinitionCreated`, `MetricDefinitionUpdated` (new version).
- `MetricValue` (root: `MetricValueId`) — Event: `MetricThresholdViolated`.
- `Milestone` (root: `MilestoneId`) — Events: `MilestoneReached`, `MilestoneSlipped`.

**Value Objects**: `MetricDimension` (Enumeration), `MetricWeight`, `MetricTarget`,
`MetricThreshold`, `DeadlineStatus`, `ProgressExplanation`, `ComponentValue`.

**Domain services**: `IProgressCalculationStrategy` (strategy pattern, per-project
selection), `IMetricEvaluationPolicy`, `IDeadlineEvaluator`.

## Application Layer (representative)

- `DefineMetric`, `UpdateMetricDefinition`, `OverrideProgressManually`,
  `CreateMilestone`, `EvaluateMilestone`.
- Queries: `GetProjectHealth(projectId)`, `GetManagerDashboard(managerId)` (subtree),
  `ExplainProgress(workItemId)` (returns the persisted explanation).

## Acceptance Criteria

- Given identical inputs, When progress is recalculated, Then the same value and explanation result (determinism).
- Given a task with 3 of 4 weighted subtasks complete, When `ExplainProgress` runs, Then the response shows each component, weight, and the arithmetic.
- Given a metric threshold violation, When evaluation runs, Then `MetricThresholdViolated` is raised and visible on the manager dashboard.
- Given a manual progress override, When saved, Then the override, its actor, and its justification are audited and included in the explanation.
- Given a manager's dashboard query, When executed, Then only subtree/membership-visible projects contribute.
- Given a historical date, When progress is queried, Then the value at that time can be reconstructed from persisted explanations.

## TDD Strategy

- **Unit**: every strategy's arithmetic, weight normalization (zero-weight sets), deadline
  transitions across time boundaries, determinism (same inputs → same output), explanation
  completeness. These are the highest-value domain tests in the platform.
- **Integration**: recalculation triggered by domain events from SPEC-003, dashboard read models.

**Constitution traceability**: Principles XII, XIII; §Versioning.

---

# SPEC-005 — Document Management

**Bounded Context**: BC-05 Documents (Core) · **Depends on**: SPEC-003 (project/work item linkage), SPEC-002 (classification-aware authorization)

## Objective

Implement enterprise document management where documents are first-class domain objects
with classification, immutable versions, and an audited access model (Constitution
Principle IX).

## Requirements

**R1 — Document aggregate**: Identity, name, classification
(`Public/Internal/Confidential/Restricted/HighlyRestricted` + organization-defined
extensions), security classification, owner, organization/tenant, project/work-item
links, current version pointer, MIME type, size, hash, status, lifecycle, provenance,
retention, access history.

**R2 — Immutable versions**: `DocumentVersion` (v1, v2, …) is immutable once published.
Corrections create a new version. Each version carries content hash, metadata snapshot,
and publication actor/time. Deletion is a lifecycle action (soft, audited), never a row erase.

**R3 — Extensible metadata**: author, department, project, tags, document type,
effective/expiration dates, source, confidentiality, retention, custom metadata bag —
modeled as a value object on the version (snapshot semantics).

**R4 — Access evaluation**: Every read/download is evaluated against Golden Rule A plus
classification: user, role, org, subtree, project membership, classification, explicit
grants. Denials are audited. Document access history is retained.

**R5 — Upload pipeline (asynchronous, outbox-driven)**:
`Upload → Validation → Virus/Security Scan → Metadata → Classification → Storage → Indexing`.
Each stage is a resumable job state (`Enumeration`); failures are explicit and retryable.
No stage blocks the HTTP request after upload acceptance.

**R6 — Storage**: Binary content goes to object storage (selection via ADR), referenced by
hash; the database stores metadata only. Documents at rest are protected per SPEC-012.

## Domain Model

**Aggregates**

- `Document` (root: `DocumentId`) — Events: `DocumentUploaded`, `DocumentClassified`,
  `DocumentAccessed`, `DocumentAccessDenied`, `DocumentDeleted`, `DocumentApproved`.
- `DocumentVersion` (root: `DocumentVersionId`) — Events: `DocumentVersionPublished`, `DocumentVersionSuperseded`.
- `DocumentProcessingJob` (root: `DocumentProcessingJobId`) — Event: `DocumentProcessingStageCompleted`, `DocumentProcessingFailed`.

**Value Objects**: `Classification`, `ContentHash`, `MimeType`, `RetentionPolicy`,
`DocumentStatus` (Enumeration + lifecycle), `ProcessingStage` (Enumeration), `MetadataSnapshot`.

**Domain services**: `IDocumentAccessPolicy` (Golden Rule A + classification),
`IClassificationPolicy` (default + org extensions, versioned rules).

## Application Layer (representative)

- `UploadDocument`, `PublishDocumentVersion`, `ClassifyDocument`, `ApproveDocument`,
  `RetryProcessingStage`, `DeleteDocument`.
- Queries: `GetDocument(id)` (authorization-filtered), `ListDocumentVersions`,
  `GetAccessHistory(id)` (auditor/owner scoped).

## Acceptance Criteria

- Given a valid upload, When accepted, Then a `Document` + `DocumentVersion` exist, storage holds the binary by hash, and processing jobs are queued via outbox — the HTTP request did not run the pipeline.
- Given a published version, When any correction is requested, Then a new version is created; the old one is retrievable and traceable, never mutated.
- Given a user outside the subtree/membership/grants, When they request a protected document, Then access is denied and audited, and no binary is served.
- Given a virus-scan failure, When the stage retries or fails permanently, Then the job state is explicit and observable (no half-classified documents).
- Given classification rules change, When new documents classify, Then the rule version used is recorded.
- Given an auditor query, When access history is requested, Then it returns reads, denials, and downloads with actors and timestamps.

## TDD Strategy

- **Unit**: version immutability rules, classification policy (incl. org extensions),
  access policy matrix, lifecycle transition legality, metadata snapshot equality.
- **Integration**: outbox-driven pipeline against real storage + scan stub, retry semantics.
- **Security matrix**: every classification × every actor type from the SPEC-013 matrix.

**Constitution traceability**: Principles VIII, IX, XV, XIX; §Document Architecture; Golden Rule B (input side).

---

# SPEC-006 — LLM and Document Intelligence

**Bounded Context**: BC-06 AI Processing (Core) · **Depends on**: SPEC-005, SPEC-010 (search/index for RAG)

## Objective

Create a controlled, traceable, provider-agnostic AI pipeline for document extraction,
classification, and analysis — with human review gates and authorized retrieval
(Constitution Principles X, XI; Golden Rule B).

## Requirements

**R1 — Technology selection per `dotnet-ai` skill (mandatory)**: The AI stack MUST follow
the technology-selection decision tree: `Microsoft.Extensions.AI` (`IChatClient`) as the
LLM abstraction for prompt→response operations; `Microsoft.Extensions.VectorData.Abstractions`
+ a provider connector for embeddings/vector search; `Microsoft.Extensions.AI.DataIngestion`
for chunking/ingestion where applicable. ML.NET only if a structured/tabular ML task is
identified. No direct provider SDK dependencies in the domain — providers plug into the
abstractions. Do NOT use an LLM for jobs ML.NET handles deterministically.

**R2 — Provider-agnostic domain**: The domain defines `ILLMProvider`, `ILLMProcessor`,
`IDocumentExtractor`, `IDocumentClassifier`, `IEmbeddingProvider` (names may follow repo
conventions); implementations live in Infrastructure and are selected by configuration.
The domain never references a provider SDK.

**R3 — Pipeline (asynchronous)**: `Document → Extraction → Normalization → Classification
→ Chunking → Indexing → Embedding → LLM Processing → Result → Validation → Human Review`.
Stages are outbox-queued jobs (SPEC-005 job machinery), traceable end-to-end with
correlation IDs, retryable on failure.

**R4 — AI operations**: summarization, classification, metadata extraction, entity
extraction, task extraction, deadline extraction, requirement extraction, risk detection,
content completeness, version comparison, question answering, project-context analysis.
Each operation type declares: input contract, prompt template version, review requirement.

**R5 — Provenance (mandatory on every result)**: `SourceDocumentId`, `SourceDocumentVersionId`,
`OperationId`, `OperationType`, `Model`, `PromptVersion`, `CreatedAt`, `CreatedBy/System`,
`ProcessingStatus`, confidence/quality indicators when available. No AI result exists
without provenance (Constitution Principle X).

**R6 — Prompt versioning**: `LlmPromptVersion` is immutable; changing a prompt creates a
new version. Historical results keep the version they were produced with.

**R7 — Human review**: AI results carry `Generated → PendingReview → Approved | Rejected |
Superseded`. Review requirements are configurable per operation type × classification ×
policy. Approved results may feed business data (e.g., extracted deadlines) but never
silently overwrite authoritative human-created values (Constitution Principle XI).

**R8 — Authorized RAG (Golden Rule B)**: Retrieval filters by the full authorization stack
BEFORE chunks reach the model. Global indexes are forbidden. Answers carry sources
(document + version + chunk), each of which the user was authorized to read.

## Domain Model

**Aggregates**

- `LlmOperation` (root: `LlmOperationId`) — Events: `LlmOperationQueued`, `LlmOperationCompleted`, `LlmOperationFailed`, `LlmOperationRetried`.
- `LlmPromptVersion` (root: `LlmPromptVersionId`) — Event: `PromptVersionPublished`.
- `LlmResult` (root: `LlmResultId`) — carries provenance VO; Events: `LlmResultGenerated`, `LlmResultApproved`, `LlmResultRejected`, `LlmResultSuperseded`.
- `LlmReview` (root: `LlmReviewId`) — reviewer, decision, rationale, timestamp.

**Value Objects**: `OperationType` (Enumeration), `Provenance`, `ReviewStatus`
(Enumeration), `ModelDescriptor`, `QualityIndicator`, `ChunkReference`.

**Domain services**: `IReviewPolicy` (when is review required), `IAuthorizedRetrievalPolicy`
(Golden Rule B gate), `IResultValidationPolicy`.

## Application Layer (representative)

- `QueueLlmOperation`, `RetryLlmOperation`, `PublishPromptVersion`, `RequestLlmReview`,
  `ApproveLlmResult`, `RejectLlmResult`, `AskDocumentQuestion` (RAG query).
- Queries: `GetOperationProvenance(operationId)`, `ListPendingReviews(reviewer)`,
  `GetResultHistory(documentVersionId)`.

## Acceptance Criteria

- Given a document the user can read, When an AI operation completes, Then the result includes full provenance (source, version, model, prompt version, actor, status).
- Given a prompt change, When published, Then a new immutable version exists and prior results still reference the old version.
- Given an operation requiring review, When the result is generated, Then it is `PendingReview` and cannot influence business data until Approved.
- Given a RAG query, When retrieval runs, Then only authorized chunks reach the model and the answer enumerates its sources.
- Given a cross-branch user, When they ask a question over content including another branch's documents, Then those documents are absent from both retrieval and the answer.
- Given a failed operation, When retried, Then the retry is idempotent (no duplicate authoritative results).
- Given any AI output, When it targets authoritative human data, Then it proposes — never overwrites.

## TDD Strategy

- **Unit**: provenance completeness, review policy matrix (operation × classification × policy), prompt immutability, result state machine.
- **Integration**: pipeline stages with mocked providers (deterministic tests per SPEC-013), outbox retries, vector-store connector behavior.
- **Security**: retrieval leakage tests — cross-branch and cross-classification queries MUST NOT surface protected chunks; prompt-injection regression suite (untrusted document content cannot command the pipeline).

**Constitution traceability**: Principles X, XI, XVII; Golden Rule B; dotnet-ai skill mandate.

---

# SPEC-007 — Audit, Monitoring and Compliance

**Bounded Context**: BC-08 Audit (Supporting) + platform monitoring · **Depends on**: SPEC-001 (all contexts emit events)

## Objective

Provide complete, append-only accountability for security and business actions, plus
operational monitoring (Constitution Principle VIII, XVIII).

## Requirements

**R1 — Append-only audit store**: `AuditEntry` records: `AuditId`, `Timestamp`, `Actor`,
`Action`, `ResourceType`, `ResourceId`, `Organization/Tenant`, `Result`, `CorrelationId`,
client metadata (where permitted), and before/after snapshots (sensitive fields masked).
Entries are never updated or deleted; corrections are new entries. Tamper-evidence
(e.g., hash chaining) SHOULD be evaluated in an ADR.

**R2 — Event catalog (minimum)**: authentication outcomes; authorization denials;
project/work-item creation and modification; assignment/status/metric changes; document
lifecycle (upload, classify, version, access, denial, delete, approve); permission and
grant changes; hierarchy changes; AI operations, results, and review decisions;
configuration changes.

**R3 — Emission path**: Audit events flow domain events → outbox → integration events →
audit consumer (idempotent). Correlation IDs from OTel propagation are embedded.

**R4 — Audit search**: Authorized managers/auditors filter by actor, action, resource,
project, organization, date range, result, correlation ID — authorization-filtered by the
same Golden Rule A (an auditor sees what their scope permits).

**R5 — Operational dashboards**: Service health, failed requests, background jobs
(document processing, AI ops), queue depth, latency, DB errors, authorization failures —
surfaced via Aspire dashboard + OTel backends (alerts via ADR).

## Domain Model

**Aggregates**

- `AuditEntry` (root: `AuditEntryId`) — immutable by design; Event: none (terminal record).

**Value Objects**: `AuditAction` (Enumeration), `ActorReference`, `ResourceReference`,
`BeforeAfterSnapshot` (masked), `AuditResult`.

**Domain services**: `IAuditMaskingPolicy` (what is sensitive), `IAuditQueryAuthorization`.

## Application Layer (representative)

- Queries: `SearchAuditEntries(filters)` (auditor-scoped), `GetAuditTrail(resourceId)`,
  `GetOperationTimeline(correlationId)`.
- Background: `AuditEventConsumer` (integration event handler, idempotent).

## Acceptance Criteria

- Given any action in the R2 catalog, When it occurs, Then an audit entry exists with actor, resource, result, and correlation ID.
- Given an attempt to modify an audit row, When attempted via any supported path, Then it fails (no update/delete path exists — enforced by model + tests).
- Given an auditor without cross-branch scope, When they search, Then other branches' entries are filtered out.
- Given a distributed document workflow, When inspected by correlation ID, Then the full timeline (HTTP → storage → processing → indexing → LLM → review) is reconstructible.
- Given a critical dependency outage, When health checks run, Then the failure is identifiable per dependency.

## TDD Strategy

- **Unit**: masking policy, query authorization composition, entry immutability (no mutators compiled).
- **Integration**: consumer idempotency (duplicate delivery → one entry), search filters, correlation propagation end-to-end.

**Constitution traceability**: Principles VIII, XVIII; §Final Rule.

---

# SPEC-008 — Notifications and Collaboration

**Bounded Context**: BC-09 Notifications (Supporting) · **Depends on**: SPEC-003, SPEC-005, SPEC-006 (event sources)

## Objective

Notify users about changes requiring attention, decoupled from business events and channels.

## Requirements

**R1 — Event-driven generation**: Notifications are derived ONLY from integration events
(assignments, reassignments, overdue, blocked, completed, review requested, document
uploaded/classified/approved, AI review requested, risk increased). No notification
logic inside other contexts.

**R2 — Channels decoupled**: `InApp` (default) + `Email` (future) + extensible channel
abstraction. Channel implementations subscribe to notification integration events; failure
in one channel never blocks others.

**R3 — Deduplication & idempotency**: Consumers are idempotent (at-least-once delivery);
duplicate events do not produce duplicate notifications (dedupe key = event ID + recipient).

**R4 — Preferences**: Per-user notification preferences (event type × channel), within
organizational policy limits.

**R5 — Content safety**: Notifications minimize sensitive content (no classified
document bodies, no AI result payloads) — metadata and links only.

## Domain Model

**Aggregates**

- `Notification` (root: `NotificationId`) — Events: `NotificationCreated`, `NotificationRead`.
- `NotificationPreference` (root: per user) — Event: `PreferencesUpdated`.

**Value Objects**: `NotificationType` (Enumeration), `Channel` (Enumeration), `DedupeKey`,
`DeliveryState` (Enumeration).

**Domain services**: `INotificationPolicy` (who gets what, preference/policy merge),
`IChannelRouter`.

## Application Layer (representative)

- Background: `NotificationDispatcher` (event → notifications), `EmailChannelConsumer`.
- Queries: `GetMyNotifications`, `GetUnreadCount`; Commands: `MarkRead`, `UpdatePreferences`.

## Acceptance Criteria

- Given a `WorkItemAssigned` integration event, When consumed, Then the assignee receives exactly one in-app notification.
- Given the same event redelivered, When consumed again, Then no duplicate is created (dedupe).
- Given a disabled email channel, When events flow, Then in-app still works; email failures are observable.
- Given a `Confidential` document approval, When the notification is composed, Then it contains metadata/link only — no content.
- Given user preferences off for a type, When an event of that type flows, Then no notification is created unless policy mandates it.

## TDD Strategy

- **Unit**: policy merge (preferences × org policy), dedupe key behavior, content safety rules.
- **Integration**: consumer idempotency, channel fan-out, failure observability (dead-letter visibility).

**Constitution traceability**: Principles XVII, XIX.

---

# SPEC-009 — API, UI and User Experience

**Bounded Contexts**: BC-10 Platform + all read models · **Depends on**: SPEC-002…SPEC-008
**Mandatory skills**: `minimal-ui-design-system`, `ngrx-signal-store`

## Objective

Provide an intuitive enterprise UI where security is enforced by the API, role/branch-aware
navigation, and a consistent design system.

## Requirements

**R1 — API contracts first**: All APIs are stable contracts (Constitution XVI):
pagination, filtering, sorting, search, optimistic concurrency (ETag/version), consistent
error responses (ProblemDetails via `Result → HTTP`), authorization, validation. Internal
entities never leak as contracts.

**R2 — Views (minimum)**: Dashboard, Projects, Kanban, Work Item Detail, My Tasks,
Team Tasks, Planning, Documents, AI Processing (review queue), Notifications, Audit,
Administration.

**R3 — Manager dashboard**: My Projects, My Team, My Sub-Managers, Overdue, Blocked,
Critical, At Risk, Completed, AI Reviews pending, Document Reviews — all subtree-filtered.

**R4 — Work item detail**: description, responsible, manager, status, progress
(with explanation link), metrics, subtasks, dependencies, documents, history, comments,
authorized AI information.

**R5 — Design system (mandatory)**: UI follows `minimal-ui-design-system` tokens —
colors, typography, spacing, radius — and above all its ELEVATION SYSTEM (flat vs.
shadow-elevated surfaces) and component patterns (nav, top bar, KPI cards, lists, badges)
from the skill's `references/` files. Consult before writing any component.

**R6 — State management (mandatory)**: Frontend state uses NgRx SignalStore per the
`ngrx-signal-store` skill: `signalStore`, `withState`, `withComputed`, `withMethods`,
`withProps`, entity features, lifecycle hooks, rxjs-interop for API calls, and its testing
patterns.

**R7 — Security posture of the UI**: The UI hides unauthorized functionality, but the API
remains the sole authority — hiding is UX, not security. Every UI action maps to an
authorized command/query.

## Acceptance Criteria

- Given a Contributor, When the app renders, Then management views are absent and the API denies them independently.
- Given a Manager, When opening the dashboard, Then counts reflect only their subtree.
- Given any list view, When a query returns, Then pagination/filter/sort contracts are honored and errors follow ProblemDetails.
- Given any stale version edit, When submitted, Then the UI surfaces a concurrency error from the API without data loss.
- Given a new screen design, When implemented, Then tokens/elevation rules come from the design system skill (verifiable in review), and state lives in a SignalStore with tests.

## TDD Strategy

- **Unit (frontend)**: store tests per skill patterns (state, computed, methods).
- **E2E**: role-based navigation, Kanban round trip, concurrency conflict UX.
- **Contract**: API contract tests (ProblemDetails shape, pagination envelope, ETag behavior).

**Constitution traceability**: Principles XVI, XIX; skill mandates (Principle XXII).

---

# SPEC-010 — Data Persistence and Search

**Bounded Contexts**: BC-07 Search & Indexing + persistence platform · **Depends on**: SPEC-001, SPEC-005

## Objective

Provide reliable persistence, efficient hierarchical queries, and authorization-aware
search that can never leak cross-branch content.

## Requirements

**R1 — Persistence requirements**: Transactional writes, optimistic concurrency (row
versions), reproducible EF migrations, audited interceptors, purposeful indexes.

**R2 — Hierarchy query strategy (ADR)**: The system MUST efficiently answer: who reports
to me, who reports to my managers, which projects/tasks belong to my subtree. The storage
strategy (PostgreSQL recursive CTE vs. closure table vs. `ltree`) is decided in an ADR
informed by SPEC-000 — recursive operations are designed deliberately, never improvised.

**R3 — Search coverage**: Projects, work items, documents, document content (indexed),
users, metadata, tags, classifications, audit (per authorization).

**R4 — Authorization-filtered search (Golden Rule B sibling)**: Search results are
filtered by the SAME authorization stack as direct access. The index MUST NOT return
snippets from documents the user cannot access. Index entries carry scope metadata
(org, subtree owner, classification, project) so filtering can be pushed into the index
query where the engine supports it; otherwise filtering is applied as a hard post-gate
that can only remove results, never add.

**R5 — Indexing pipeline reliability**: Indexing is an outbox-driven async job; failures
are explicit, observable, retryable, and recoverable (reindex from source of truth).

## Domain Model

- `DocumentIndex` (root: `DocumentIndexId`) — indexing state per document version; Events:
  `DocumentIndexed`, `IndexingFailed`.
- Search context has no mutable business aggregates — it is a projection/read side.

## Application Layer (representative)

- Queries: `SearchProjects`, `SearchWorkItems`, `SearchDocuments`, `SearchDocumentContent`,
  `SearchUsers`, `SearchAudit`.
- Commands: `ReindexDocument(versionId)`, `RebuildIndex(scope)` (admin).

## Acceptance Criteria

- Given migrations on a clean environment, When applied, Then the schema is reproducible.
- Given two concurrent edits, When saved, Then exactly one wins and the other gets a concurrency error.
- Given a manager's subtree query over 4+ levels of hierarchy, When executed, Then it returns correct results within the performance envelope defined by the ADR.
- Given a cross-branch user searching protected content, When results return, Then no snippets, no titles, and no counts from protected documents appear.
- Given an indexing failure, When retried/rebuilt, Then the index converges with the source of truth.

## TDD Strategy

- **Unit**: scope-metadata composition for index queries; post-gate filter (removes only).
- **Integration**: hierarchy strategy performance tests (seeded deep org), search
  authorization leakage suite (mirror of SPEC-006 RAG tests at the search layer),
  migration reproducibility, reindex convergence.

**Constitution traceability**: Principles XV, XIX; Golden Rule A/B.

---

# SPEC-011 — Observability and Resilience

**Bounded Context**: BC-10 Platform · **Depends on**: SPEC-001

## Objective

Make the distributed system diagnosable and resilient (Constitution Principle XVIII).

## Requirements

**R1 — Telemetry**: Every service emits structured logs (Serilog), traces, metrics,
health status via ServiceDefaults + OTel/OTLP. No service ships without them.

**R2 — Correlation**: HTTP requests and background operations carry trace/correlation
identifiers end-to-end; the document workflow (`HTTP Upload → Storage → Processing →
Extraction → Indexing → LLM → Review`) is traceable as one timeline.

**R3 — Safe retries only**: Retries apply only to known-safe operations; handlers are
idempotent (at-least-once event bus). Retry storms are prevented (bounded retries,
exponential backoff, circuit breakers via standard HTTP resilience in ServiceDefaults).

**R4 — Background job observability**: Queue depth, job states, failure rates are
visible; dead-lettered events are inspectable.

## Acceptance Criteria

- Given any service, When it starts, Then `/health` and `/alive` respond and telemetry flows to the configured OTLP endpoint.
- Given a full document workflow, When queried by correlation ID, Then all stages appear in one trace.
- Given a downstream outage, When retries exhaust, Then the circuit opens (no storm), the failure is visible, and the operation is resumable.
- Given a poison message, When retries fail, Then it is dead-lettered with full context — never silently dropped.

## TDD Strategy

- **Integration**: resilience policy verification (retry/backoff/circuit-open) with fault-injected
  dependencies; correlation propagation assertions.

**Constitution traceability**: Principle XVIII; §Asynchronous Processing.

---

# SPEC-012 — Security

**Bounded Context**: Cross-cutting (all) · **Depends on**: SPEC-002 (authorization model)

## Objective

Establish security controls for identity, authorization, and protected enterprise
information — deny by default, fail closed (Constitution Principle XIX).

## Requirements

**R1 — Authentication**: Delegated entirely to `oroidentityserver` (OIDC authorization
code + refresh). No local credentials, no password handling, no custom token issuance.

**R2 — Authorization**: The full Golden Rule A stack (Role + Permission + Organization +
ManagementHierarchy + Ownership + ProjectMembership + Classification) — never role checks alone.

**R3 — Data protection**: Sensitive data is protected in transit, at rest, in logs, in
backups, and in AI pipelines. Audit masking covers PII/classified fields. Document binaries
are stored with protection appropriate to their classification (ADR: encryption-at-rest scope).

**R4 — Document security**: The application MUST prevent unauthorized downloads,
unauthorized indexing, unauthorized AI retrieval, and cross-organization access (Golden
Rule B is a security requirement, not a feature).

**R5 — Secrets**: Never in source code, committed configuration, logs, prompts, or
telemetry. Local dev secrets use user-secrets; container config via env vars (as
oroidentityserver's image pattern demonstrates).

**R6 — Security test matrix (mandatory suite)**: Privilege escalation (vertical),
horizontal access violations, cross-organization access, document leakage, AI retrieval
leakage, expired/invalid tokens, missing permissions — automated, failing CI when red.

**R7 — Fail closed**: All protected endpoints fail closed when authorization information
is insufficient (no `sub`/roles/tenant → deny).

## Acceptance Criteria

- Given any protected endpoint with an invalid/expired token, When called, Then it returns 401/403 — never data.
- Given a token without `tenant_id`, When a scoped query runs, Then access is denied (fail closed).
- Given every row of the SPEC-013 security matrix, When executed, Then actual access matches the defined expectation.
- Given a classified document in a RAG query from a lower-privilege user, Then neither retrieval nor answer contains it.
- Given a secrets scan of the repo and images, When run, Then no secret material is found.

## TDD Strategy

- **Integration/E2E**: the R6 matrix as a first-class automated suite (see SPEC-013 for the matrix table).

**Constitution traceability**: Principle XIX; Golden Rules A & B; §Security by Default.

---

# SPEC-013 — Testing and Quality

**Bounded Context**: Cross-cutting · **Depends on**: all specs (this spec defines the shared test discipline)

## Objective

Guarantee correctness of business rules and security boundaries through TDD
(Constitution Principles XX, XXI).

## Requirements

**R1 — Test stack**: Follow the repository's established stack (per SPEC-000 discovery:
xUnit, NSubstitute, Testcontainers, EF InMemory where appropriate). No new test
framework without an ADR.

**R2 — TDD cycle**: Red → Green → Refactor enforced for domain/application behavior;
tests are written before implementation for domain rules, transitions, and policies.

**R3 — Test levels**:
- **Unit**: progress/metric calculations, state transitions, hierarchy rules, document
  version rules, classification rules, authorization evaluator composition.
- **Integration**: database, identity (containerized OroIdentityServer), authorization,
  document storage, search, background jobs, event bus.
- **E2E**: the canonical chain —
  ```
  Root Manager → creates Manager → Manager creates project → creates tasks
  → assigns subordinate → subordinate updates task → Manager observes progress
  → subordinate uploads document → document indexed → LLM analyzes
  → Manager reviews result
  ```
- **LLM workflows**: deterministic tests with mocked providers (fakes behind the MEAI
  abstractions); no test calls a real provider.

**R4 — Security test matrix (explicit expectations)**: For every major resource
(`Project`, `WorkItem`, `Document`, `LlmResult`, `AuditEntry`, `ManagementRelationship`),
define and assert access for:

| Actor \ Resource | Project | WorkItem | Document | LlmResult | Audit | Hierarchy |
|---|---|---|---|---|---|---|
| Owner | rw | rw | rw | read | read(own) | n/a |
| Manager (same subtree) | rw (scoped) | rw (scoped) | read | review | read(subtree) | manage |
| Manager's Manager | read/write via subtree | read/write via subtree | read | review | read(subtree) | manage |
| Peer Manager (other branch) | none* | none* | none* | none* | none* | none |
| Different Branch subordinate | none* | none* | none* | none* | none* | none |
| Auditor | read | read | read(perm.) | read | read(scope) | read |
| Administrator | admin | admin | admin | admin | read | admin |
| Anonymous | none | none | none | none | none | none |

\* unless explicit grant or shared project membership exists (then per grant).

**R5 — Authorization boundary tests**: Hierarchy boundaries (Golden Rule A) have
dedicated tests in every context that returns protected data — list, search, dashboard,
RAG, audit search.

## Acceptance Criteria

- Given a domain rule change, When CI runs, Then the corresponding unit tests fail first (TDD evidence in history).
- Given the security matrix, When executed, Then every row passes or the expectation was formally revised.
- Given an LLM workflow test, When run, Then it completes deterministically with mocked providers.
- Given the E2E chain, When run against the composed environment, Then it passes end-to-end including audit entries.

## TDD Strategy

This spec IS the strategy; it is executed through all other specs' TDD sections.

**Constitution traceability**: Principles XX, XXI; §Definition of Done.

---

# SPEC-014 — Deployment and Operations

**Bounded Context**: BC-10 Platform · **Depends on**: all specs (operational readiness)

## Objective

Define repeatable execution from local development to production.

## Requirements

**R1 — Local environment**: .NET 10 + Aspire + Podman + external `oroidentityserver`
(container). The AppHost composes: PostgreSQL (Npgsql), RabbitMQ, Redis, module
services, Web frontend, and the identity server as an external container resource with
env-var configuration (mirroring oroidentityserver's documented Podman pattern:
connection strings, seed admin, security keys via env).

**R2 — Configuration**: Externally configurable: Identity URL, database, storage, search,
messaging, LLM provider, embedding provider, telemetry. Four environments:
Development/Test/Staging/Production — values never hard-coded (Constitution §Configuration).

**R3 — Containers**: Services are containerizable; images are reproducible
(slim runtime base, non-root user — same pattern as oroidentityserver's Dockerfile).

**R4 — Database initialization**: Migrations run automatically/consistently per the
repo's established mechanism; reproducible on a clean environment.

**R5 — Secrets in deployment**: No production secret is embedded in images; compose files
reference env/secret stores only.

**R6 — Backups**: Documented strategy for database, document storage, search/index data
(where reconstruction is expensive), and audit data.

**R7 — Recovery procedures**: Documented for database failure, storage failure, search
failure, AI provider failure, message processing failure, and identity server
unavailability (degraded mode: what still works when identity is down).

## Acceptance Criteria

- Given a clean machine with Podman + .NET 10, When following the README, Then the full environment (including identity) runs via `aspire start`.
- Given any environment, When configuration is externalized properly, Then swapping environments requires no code/image changes.
- Given the production image, When scanned, Then it contains no secrets and runs as non-root.
- Given each failure mode in R7, When the documented procedure is followed, Then recovery is achievable and data loss scope is stated.

## TDD Strategy

- **Integration**: environment reproducibility test (compose-up + health probe),
  migration-from-clean test, backup/restore rehearsal scripts (documented and executable).

**Constitution traceability**: Principles II, IV, XIX; §Configuration.

---

# Sprint Roadmap

Aligned with Constitution §17 (Initial Delivery Strategy) and the dependency graph above.
The system MUST remain usable at each milestone.

| Phase | Specs | Deliverable / Exit criteria |
|---|---|---|
| **Sprint 0 — Discovery** | SPEC-000 | Discovery document + ADR queue; no feature code |
| **Sprint 1 — Foundation** | SPEC-001 | Solution skeleton builds, AppHost composes modules + infra + external identity, architecture tests green |
| **Sprint 2 — Identity & Org** | SPEC-002 | OIDC sign-in works; hierarchy + evaluator + grants; security matrix rows green for hierarchy |
| **Sprint 3 — Projects & Kanban** | SPEC-003 | Create projects/work items, board renders, transitions + dependencies validated, concurrency enforced |
| **Sprint 4 — Metrics & Progress** | SPEC-004 | Configurable metrics, explainable deterministic progress, manager dashboards (subtree-scoped) |
| **Sprint 5 — Documents** | SPEC-005 | Upload pipeline (async), versioning, classification, access control + audit |
| **Sprint 6 — Search** | SPEC-010 | Authorization-filtered search incl. content; hierarchy ADR implemented and performant |
| **Sprint 7 — LLM** | SPEC-006 | Provider-agnostic pipeline, provenance, prompt versioning, review gates, authorized RAG |
| **Sprint 8 — Audit & Observability** | SPEC-007, SPEC-011 | Append-only audit + search; full telemetry, correlation, resilience |
| **Sprint 9 — Notifications & UI** | SPEC-008, SPEC-009 | Event-driven notifications; design-system UI over SignalStore |
| **Sprint 10 — Hardening & Ops** | SPEC-012, SPEC-013, SPEC-014 | Security matrix green, E2E chain green, environment reproducibility + recovery docs |

---

# Risks & Technical Debt (MVP deferrals)

| # | Risk / Deferral | Mitigation |
|---|---|---|
| 1 | Hierarchy storage strategy not yet chosen (CTE vs closure vs ltree) | ADR-002 decided in Sprint 0/1 with performance test seeding (SPEC-010 R2) |
| 2 | Vector store / embedding provider not yet chosen | ADR decided with SPEC-006 start, per `dotnet-ai` skill options; abstractions isolate the choice |
| 3 | Object storage + encryption-at-rest scope | ADR with SPEC-005; classification-dependent protection level |
| 4 | Email channel | Deferred post-MVP (SPEC-008 R2 abstraction keeps the seam) |
| 5 | Audit tamper-evidence (hash chaining) | ADR evaluates cost/benefit; append-only enforced from day one regardless |
| 6 | LLM provider costs / rate limits | Idempotent jobs + retries + circuit breaking (SPEC-011); mocked providers in tests |
| 7 | Prompt-injection via document content | Security suite in SPEC-006 TDD; AI output never authoritative without review |
| 8 | UI scope creep on dashboards | Design-system skill tokens fixed early (SPEC-009 R5); incremental views |

---

# ADR Checklist (Constitution §15)

Decisions that MUST have ADRs, in expected order:

| ADR | Decision | Triggered by |
|---|---|---|
| ADR-000 | Discovery findings → capability/gap matrix | SPEC-000 |
| ADR-001 | Composition host model (single API host vs. per-module hosts) | SPEC-001 |
| ADR-002 | Hierarchy query/persistence strategy | SPEC-002/010 |
| ADR-003 | Module boundary enforcement mechanism (beyond architecture tests) | SPEC-001 |
| ADR-004 | Object storage + at-rest protection | SPEC-005 |
| ADR-005 | LLM + embedding providers and vector store | SPEC-006 |
| ADR-006 | Audit tamper-evidence approach | SPEC-007 |
| ADR-007 | Search engine selection | SPEC-010 |
| ADR-008 | Email/external channel providers | SPEC-008 |
| ADR-009 | Alerting / OTel backend | SPEC-007/011 |
| ADR-010 | Environment/secret store for staging-production | SPEC-014 |

---

**End of refined specifications.**
