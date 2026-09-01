# Quickstart: Identity, Access and Organization Validation

**Feature**: 003-identity-access-organization | **Date**: 2026-08-31

## Prerequisites

- Foundation spec 002 is complete: `dotnet build OroKanban.slnx -warnaserror` 0 warnings, `OroKanban.AppHost` declares Postgres, RabbitMQ, Redis, external `oroidentityserver` (Authority via `Identity__Authority`)
- `oroidentityserver` Podman container is running and reachable at the Authority (discovery at `GET {Authority}/.well-known/openid-configuration`); client for `authorization_code` + `refresh_token` grants has been registered out of band
- EF Core migrations for `OrganizationDbContext` (schema `organization`) and `IdentityDbContext` (permission catalog) have been applied via `dotnet ef database update` per 002's persistence convention
- App is running: `aspire run` or `dotnet run --project src/Api/Api.csproj`

## Setup

```bash
# Apply migrations for the two new contexts (run once after 002)
dotnet ef database update --project src/Modules/Organization/Organization.Infrastructure/Organization.Infrastructure.csproj --startup-project src/Api/Api.csproj
dotnet ef database update --project src/Modules/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj --startup-project src/Api/Api.csproj

# Seed the permission catalog (IHostedService runs on first Api start; verify)
curl -s http://localhost:5000/api/permissions | jq . | head -n 30
# Expected: seeded permissions including project.read, workitem.assign, document.approve, etc.

# Run tests
dotnet test tests/Architecture -v minimal           # existing guard + hierarchy boundary checks
dotnet test src/Modules/Organization/Organization.Tests -v minimal  # new: cycle, grant, evaluator tests (see below)
```

## Verify

### 1. Hierarchy — cycle prevention and subtree

```bash
# Create users in OroIdentityServer (via its admin API or UI), capture their sub ids from tokens
# Then via OroKanban Api (authenticated, e.g., curl with bearer token):

# Assign Alice → Bob (Manager type, same unit)
curl -s -X POST http://localhost:5000/api/organization/relationships \
  -H "Authorization: Bearer <Alice-token>" -H "Content-Type: application/json" \
  -d '{"managerId":"<Alice-sub>","subordinateId":"<Bob-sub>","type":"Manager","organizationUnitId":"<unit>"}' | jq .

# Build A→B→C, then attempt C→A (cycle)
curl -s -X POST http://localhost:5000/api/organization/relationships \
  -H "Authorization: Bearer <C-token>" -H "Content-Type: application/json" \
  -d '{"managerId":"<C-sub>","subordinateId":"<A-sub>","type":"Manager"}' | jq .
# Expected: 400 with Error.Validation "Subtree cannot contain manager" and no row inserted

# Subtree probes
curl -s "http://localhost:5000/api/organization/managers/<A-sub>/subtree" -H "Authorization: Bearer <A-token>" | jq .
# Expected: [B, C] (and transitive) — A is not in its own subtree

curl -s "http://localhost:5000/api/organization/users/<C-sub>/ancestors" -H "Authorization: Bearer <A-token>" | jq .
# Expected: [B, A] in ancestor order
```

### 2. Authorization — Golden Rule A and cross-branch isolation

Seed a hierarchy Root→ManagerA→{A1, A2, M-A1→their reports} and ManagerB in another branch, plus tasks owned by A1/A2.

```bash
# Manager A queries tasks — sees subtree + explicit-grant + project-member
curl -s "http://localhost:5000/api/projects/<project>/workitems" -H "Authorization: Bearer <ManagerA-token>" | jq 'length'
# Expected: count equals subtree-owned items plus grant-covered plus project-member items

# Manager B queries the same project — sees nothing (absent, not error-leaking)
curl -s "http://localhost:5000/api/projects/<project>/workitems" -H "Authorization: Bearer <ManagerB-token>" | jq .
# Expected: [] and HTTP 200 (not 403), no error body revealing A's items

# Policy probe (auditor/manager)
curl -s "http://localhost:5000/api/authorization/can-perform" \
  -H "Authorization: Bearer <B-token>" -H "Content-Type: application/json" \
  -d '{"permission":"workitem.read","resourceType":"WorkItem","resourceId":"<A1-task>"}' | jq .
# Expected: { isAllowed: false } — deny reason is audited but not returned

# Grant expiry probe
curl -s -X POST http://localhost:5000/api/organization/grants \
  -H "Authorization: Bearer <A-token>" -H "Content-Type: application/json" \
  -d '{"granteeUserId":"<B-sub>","resourceType":"WorkItem","resourceId":"<A1-task>","permission":"workitem.read","expiresAt":"2020-01-01T00:00:00Z"}' | jq .
# Then CanActorPerform for B on that task → deny (IsExpired true)
```

### 3. Tenant isolation (first gate)

```bash
# Two tenants: use tokens with different tenant_id claims on resources with TenantId set
curl -s "http://localhost:5000/api/projects/<tenantA-project>/workitems" -H "Authorization: Bearer <tenantB-token>" | jq .
# Expected: [] — deny on TenantMismatch before any subtree/permission work
```

### 4. Audited denials and cache invalidation

```bash
# Trigger a deny, then check audit store
curl -s "http://localhost:5000/api/authorization/can-perform" \
  -H "Authorization: Bearer <B-token>" -H "Content-Type: application/json" \
  -d '{"permission":"workitem.read","resourceType":"WorkItem","resourceId":"<A1-task>"}' | jq .

curl -s "http://localhost:5000/api/audit?resourceId=<A1-task>&action=authorization.denied" \
  -H "Authorization: Bearer <Auditor-token>" | jq '.[0] | {actor, permission, tenant, correlationId}'
# Expected: audit entry with actor=B, permission=workitem.read, tenant, non-empty correlationId — via outbox, same transaction as the deny

# Hierarchy-change cache invalidation
curl -s "http://localhost:5000/api/organization/managers/<A-sub>/subtree" -H "Authorization: Bearer <A-token>" | jq 'length'
# Note count, then assign a new subordinate D to A, then query subtree again — count should increment without restarting the service
```

### 5. Health and hierarchy storage contract

```bash
curl -s http://localhost:5000/health | jq .
curl -s http://localhost:5000/api/platform/health | jq .identity
# Expected: identity.reachable == true (discovery reachable); modules Healthy

# Hierarchy storage is recursive CTE — verify via spec contract
curl -s "http://localhost:5000/api/organization/managers/<A-sub>/subtree" -H "Authorization: Bearer <A-token>" | jq 'length'
# For 1,000-user seeded hierarchy, p95 <50 ms with warm Redis; 500 ms p95 for task list with subtree composition (SC-002) — measure with `time` or k6
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| AssignManager succeeds but `GetAncestors` does not contain the manager | `HasDefaultSchema("organization")` missing or migration not applied | `dotnet ef database update` for OrganizationDbContext; verify table `organization.management_relationships` exists |
| Cycle not rejected | `SubtreeCannotContainManagerRule` not wired via `CheckRule` in `ManagementRelationship` | Ensure domain event handler calls `hierarchy.GetAncestors` before insert |
| Cache still stale after hierarchy change | `OrganizationHierarchyChangedIntegrationEvent` not invalidating Redis keys | Verify `HierarchyCacheInvalidator` is registered as `IIntegrationEventHandler` and Redis `WithReference(redis)` in AppHost |
| CanActorPerform returns allow for cross-branch without grant | `SubtreeSpecification<T>` not composed before fetch | Check query handler composes `new SubtreeSpecification<T>(actorId, tenantId)` via `And` before `repository.ListAsync` |
| Tenant mismatch not denied first | Evaluator order wrong | Verify `IAuthorizationEvaluator` checks tenant before permission/subtree |
| OIDC `tenant_id` missing | `TenantClaimsTransformation` not registered or discovery not propagating `tenant_id` | Check `Api/Tenant/TenantClaimsTransformation` + `AddScoped<IClaimsTransformation>` and `Api/Program.cs` JWT options |

## What is NOT validated here

- Rich project/task domain logic (specs `003` onward) — hierarchy and evaluator are tested via the frozen hierarchy and policy probes, not via real projects/tasks.
- Frontend UI (no changes in this spec).
- Hierarchy storage ADR choice beyond recursive CTE — the contract is storage-agnostic; the ADR `docs/adr/adr-004-hierarchy-storage.md` records the CTE decision.
