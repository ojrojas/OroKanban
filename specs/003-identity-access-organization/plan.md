# Implementation Plan: Identity, Access and Organization

**Branch**: `003-identity-access-organization` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md) | **Depends on**: 002-foundation-architecture (solution, AppHost, AppDbContextBase)

**Input**: Feature specification — BC-01 Identity & Access + BC-02 Organization. R1 identity consumed via oroidentityserver OIDC, R2 permission catalog + role mapping, R3 ManagementRelationship aggregate with cycle prevention, R4 IManagementHierarchy Shared Kernel, R5 AuthorizationEvaluator (Golden Rule A), R6 cross-branch isolation via subtree Specification, R7 audited deny decisions.

## Summary

Implement the app-owned hierarchical authorization that makes Golden Rule A enforceable. Identity remains consumed (JWT `sub`/`tenant_id`/roles from oroidentityserver discovery, no local login). The app owns the permission catalog (seeded, not hard-coded), the `ManagementRelationship`/`OrganizationUnit`/`ExplicitGrant` aggregates (cycle prevention via `CheckRule`, arbitrary depth), the `IManagementHierarchy` Shared Kernel contract (`IsInSubtree`/`GetSubtree`/`GetAncestors`/`GetCommonAncestor` with Redis cache + invalidation on `OrganizationHierarchyChangedIntegrationEvent`), and a single `IAuthorizationEvaluator` composing Identity+Role/Permission+Tenant+Subtree+ProjectMembership+Ownership+Classification. Every list/search/dashboard composes a subtree `Specification<T>` before fetch; denials are audited via outbox. Scaffolding uses platform CLIs per FR-010 where new projects are needed (none expected — modules already exist from 002), but new vertical slices are `dotnet new` classlib-style files.

## Technical Context

**Language/Version**: C# .NET 10 (SDK 10.0.400 per `global.json`), TypeScript Angular latest (frontend not touched by this spec)

**Primary Dependencies**: `BuildingBlocks.Kernel.Domain` (AggregateRoot, StronglyTypedId, IBusinessRule, Specification<T>, Result/Error, IRepository), `BuildingBlocks.CQRS` (ICommand/IQuery, ISender, IPipelineBehavior), `BuildingBlocks.EventBus` + `RabbitMQ` (IntegrationEvent, IEventBus), `BuildingBlocks.ServiceDefaults` (already wired), `Npgsql.EntityFrameworkCore.PostgreSQL` + `Microsoft.EntityFrameworkCore` via `AppDbContextBase`/`OutboxEntityTypeConfiguration`, `Microsoft.AspNetCore.Authentication.JwtBearer` (already in Api), `StackExchange.Redis` via Aspire Redis (IManagementHierarchy cache), `NetArchTest.Rules` for arch guard (already in tests/Architecture)

**Storage**: PostgreSQL via Aspire `postgres` (single DB, per-module schema `identity`/`organization` — see research Decision 1). Redis via Aspire `redis` for `IManagementHierarchy` subtree caches. Outbox table `outbox_messages` per `AppDbContextBase`.

**Testing**: xUnit (`dotnet test`), NetArchTest reflection, Testcontainers for Postgres/Redis, `NSubstitute` for `IClaimsTransformation` / `IManagementHierarchy` fakes, `Microsoft.AspNetCore.TestHost` for Api JWT claim mapping. TDD per spec: unit (cycle rules, grant expiry, evaluator composition, IsSatisfiedBy), integration (hierarchy storage + Redis invalidation + OIDC claim mapping), security matrix (Owner/Manager/Manager'sManager/Peer/DifferentBranch/Auditor/Admin/Anonymous).

**Target Platform**: Linux containers via Podman (Aspire dashboard), `oroidentityserver` external container reference from AppHost (already declared in 002)

**Project Type**: Modular monolith — this feature touches `src/Modules/Identity` (app-owned permission mapping) and `src/Modules/Organization` (hierarchy + grants) plus `src/Api` wiring (JWT tenant propagation, authorization policy registration); `src/Modules/*` consumers use `IManagementHierarchy` Shared Kernel

**Performance Goals**: `IsInSubtree`/`GetSubtree` <50 ms p95 on 1,000-user hierarchy with warm Redis; 500 ms p95 end-to-end task query with subtree composition on same scale (SC-002); cycle check <100 ms (SC-003); audit outbox within same transaction (SC-004)

**Constraints**: Principle II: no local password/login — only discovery validation; Principle VII: unbounded depth, deny by default, every query filtered before fetch; Principle VIII: deny audits append-only via outbox; Principle XXI: domain rules in Domain via `CheckRule`/`IBusinessRule`, vertical slices via `ICommand`/`IQuery` + `IEndpoint` + `Result`; FR-010: any new file/project via platform CLIs (`dotnet new classlib` for new slices if needed, not manual copy); tenant isolation is first gate (R5)

**Scale/Scope**: 9 modules × 4 layers already scaffolded; this feature adds ~6 aggregates/VOs + 2 domain services + ~7 vertical slices + 1 Shared Kernel contract + 1 permission catalog seed + 3 security-matrix test suites; estimated ~2–3 sprints per roadmap (Hierarchy → Evaluator → Audit/Cache)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **I — Existing Assets Authoritative**: Reuses `draft/libraries/buildingblocks.md` (StronglyTypedId, AggregateRoot, Specification, Result, ISender, AppDbContextBase, EventBus) + `.agents/skills/ddd-project-planner` bounded contexts; no MediatR/MassTransit/AutoMapper. Discovery gate `draft/discovery/000-repository-catalog.md` is the source for hierarchy storage ADR.
- [x] **II — oroidentityserver Mandatory**: Consumed only (OIDC discovery, `sub`/`tenant_id`/roles, no local login/token issuance). `Api/Program.cs` already validates via discovery; this feature only propagates `tenant_id` as `TenantContext`.
- [x] **V — Modular Architecture**: `IManagementHierarchy` is the Shared Kernel contract from `Organization.Contracts`; no cross-module Infrastructure refs — only Contracts + EventBus. `Identity` module owns permission mapping, `Organization` owns hierarchy/grants.
- [x] **VI — Domain Rules Belong to the Domain**: Cycle prevention, manager≠subordinate, single-active-per-unit are `IBusinessRule` via `CheckRule` in `ManagementRelationship`; evaluator policy is a domain service, not a controller.
- [x] **VII — Hierarchical Authorization**: Unbounded depth, RBAC+resource-based, every query composes subtree `Specification<T>` before fetch; dedicated authorization tests per security matrix.
- [x] **VIII — Everything Important Is Auditable**: Deny decisions emit `audit.authorization.denied` via outbox; `ManagerAssignedToUser`/`GrantIssued` etc. are domain events → integration events → audit.
- [x] **XV — Tenant/Organization Aware**: `tenant_id` claim from `/connect/userinfo` is first gate in `IAuthorizationEvaluator`; every `Specification<T>` is tenant-scoped.
- [x] **XIX — Security by Default**: Fail-closed on missing tenant, least privilege, no secret in code/logs, explicit `ExplicitGrant` for cross-branch, deny reasons not leaked.
- [x] **XX — Testability Is Architectural**: Unit (cycle, grant expiry, evaluator), integration (hierarchy storage, Redis invalidation, OIDC claim mapping), security matrix (8 actor types) — all required.
- [x] **XXI — TDD+DDD+Vertical Slices**: Aggregates as `AggregateRoot<StronglyTypedId>`, vertical slices as `ICommand`/`IQuery` + `Validator` + `Handler` + `IEndpoint` via `BuildingBlocks.CQRS`, manual mapping.
- [x] **I/XXII — Skills Govern Design**: `ddd-project-planner` defines the bounded contexts used; no contradiction with `minimal-ui-design-system`/`ngrx-signal-store` (frontend not touched).

**Result: PASS — no violations, no complexity exceptions required.** Re-check after Phase 1 expected to remain PASS (only adds domain documentation, no new gate).

## Project Structure

### Documentation (this feature)

```text
specs/003-identity-access-organization/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── hierarchy-contract.md          # IManagementHierarchy + OrganizationHierarchyChangedIntegrationEvent
│   ├── authorization-contract.md      # IAuthorizationEvaluator + CanActorPerform + subtree Specification usage
│   └── permission-catalog-contract.md # PermissionCode + seeded role→permission map
└── checklists/
    └── requirements.md  # Spec quality checklist (created by /speckit.specify)
```

### Source Code (repository root)

```text
src/
├── BuildingBlocks/
│   └── BuildingBlocks.Kernel.Domain/Persistence/AppDbContextBase.cs  # already from 002 — no change except OrganizationDbContext uses it
├── Modules/
│   ├── Identity/
│   │   ├── Identity.Domain/          # Permission, PermissionCode, RolePermissionMap (seed, not hard-coded)
│   │   ├── Identity.Application/     # `CanActorPerform` probe query (policy probe)
│   │   ├── Identity.Infrastructure/  # IdentityDbContext (if any app-owned tables) + permission seeding via IHostedService
│   │   └── Identity.Contracts/       # IPermissionCatalog contract (consumed by Organization evaluator)
│   └── Organization/
│       ├── Organization.Domain/      # ManagementRelationship, OrganizationUnit, ExplicitGrant + VOs + IBusinessRules + domain events
│       ├── Organization.Application/ # AssignManager, MoveOrganizationUnit, IssueExplicitGrant/Revoke (commands), GetSubtree/WhoReportsToMe/CanActorPerform (queries)
│       ├── Organization.Infrastructure/# OrganizationDbContext (AppDbContextBase + Npgsql + HasDefaultSchema("organization") + RowVersion) + IManagementHierarchy impl (recursive CTE — see research) + Redis cache + EfRepository
│       └── Organization.Contracts/   # IManagementHierarchy Shared Kernel + OrganizationHierarchyChangedIntegrationEvent (already exists from 002, extended)
├── Api/
│   ├── Configuration/IdentityOptions.cs  # already from 002 — extended to propagate tenant_id via TenantContext
│   ├── Tenant/TenantContext.cs           # already from 002 — consumed by evaluator
│   └── Features/Organization/            # vertical slices for hierarchy/grant endpoints (optional at this stage — may live in Organization.Application)
└── tests/
    ├── Architecture/                     # existing guard — extended with hierarchy boundary checks
    └── Organization.Tests/ (or tests/Unit + Integration)  # new: OrganizationManagementTests, AuthorizationEvaluatorTests, ExplicitGrantTests
```

**Structure Decision**: App-owned hierarchy and authorization live in `Organization` (BC-02) as the Shared Kernel publisher; `Identity` (BC-01) owns the permission catalog but stores no passwords. No new projects are scaffolded (all 9×4 layers already exist from 002) — this feature adds domain files and vertical slices inside those projects via `dotnet new classlib` style file creation per FR-010 where a new slice folder is needed. All cross-module hierarchy queries go through `Organization.Contracts/IManagementHierarchy`, never direct DbContext. Redis is via Aspire `redis` (already declared).

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
