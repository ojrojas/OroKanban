# Contract: Projects API

**Module**: `Projects` (BC-03) | **Slices**: `Projects.Application/Features/Projects/*` | **Base path**: `/api/projects` | **Auth**: Bearer JWT (OIDC discovery, `tenant_id` claim via `TenantContext`) | **Cross-module read**: `Organization.Contracts/IManagementHierarchy` is consumed for visibility probes, never directly.

## POST /api/projects — CreateProject

**Command**: `CreateProjectCommand : ICommand<Result<CreateProjectResponse>>`

```json
// Request
{
  "name": "Revamp checkout",
  "description": "Q4 checkout modernization",
  "ownerId": "guid (optional, defaults to actor)",
  "managerId": "guid",
  "status": "Active",
  "priority": "High",
  "criticality": "High",
  "startDate": "2026-09-01T00:00:00Z",
  "dueDate": "2026-12-31T00:00:00Z"
}
// Response 201 Created — Location: /api/projects/{id}
{
  "id": "guid (ProjectId)",
  "tenantId": "guid",
  "name": "Revamp checkout",
  "status": "Active",
  "priority": "High",
  "criticality": "High",
  "ownerId": "guid",
  "managerId": "guid",
  "version": 1
}
// Errors: 400 Validation (name required/unique per tenant, dates valid, status/priority in Enumeration), 403 Generic denial (evaluator denies project.create), 401 unauthenticated
```

**Domain**: `Project.Create(name, ownerId, managerId, ...)` → raises `ProjectCreated` → outbox → `ProjectCreatedIntegrationEvent {ProjectId, TenantId}`. `Version` / `RowVersion` initialized. Audit via same transaction.

## POST /api/projects/{projectId}/members — AddProjectMember

**Command**: `AddProjectMemberCommand : ICommand<Result<AddProjectMemberResponse>>`

```json
// POST /api/projects/{projectId}/members
{
  "userId": "guid (must exist in OroIdentityServer)",
  "role": "Contributor"
}
// Response 200 OK (or 201)
{ "projectId": "guid", "userId": "guid", "role": "Contributor", "joinedAt": "2026-09-01T..." }
// Errors: 400 (unknown user, role not in Enumeration, duplicate member), 403 (evaluator denies organization.manage or project.update), 404 project not found (generic 404, never leaks tenant existence)
```

Raises `ProjectMemberAdded` → outbox. Member immediately satisfies `IProjectMembership.IsMember` for Golden Rule A.

## DELETE /api/projects/{projectId}/members/{userId} — RemoveProjectMember

Raises `ProjectMemberRemoved`. Same auth as add.

## PATCH /api/projects/{projectId}/status — ChangeProjectStatus

```json
{ "toStatus": "OnHold" }
// Errors: 400 transition not allowed (if policy restricts)
```
Raises `ProjectStatusChanged`.

## GET /api/projects/{projectId} — GetProjectDetail

**Query**: `GetProjectDetailQuery(projectId) : IQuery<Result<ProjectDetailResponse>>`

```json
{
  "id": "guid", "name": "...", "status": "Active", "priority": "High",
  "criticality": "High", "ownerId": "guid", "managerId": "guid",
  "members": [{ "userId": "guid", "role": "Contributor", "joinedAt": "..." }],
  "milestones": [{ "id": "guid", "title": "...", "dueDate": "...", "isReached": false }],
  "dueDate": "...", "tenantId": "guid", "updatedAt": "..."
}
```

**Authorization**: Composed before fetch — `AuthorizedProjectSpec(actorId, tenantId)` via evaluator + `IManagementHierarchy`/membership; cross-tenant access returns `404` (not 403 that leaks existence).

## POST /api/projects/{projectId}/milestones — AddMilestone

```json
{ "title": "M1 — Design sign-off", "dueDate": "2026-10-15T00:00:00Z" }
// → MilestoneReached is raised when IsReached transitions to true
```

## Conventions for all project endpoints

- **Contracts**: DTOs in `Projects.Contracts` (never domain entities).
- **Mapping**: Manual in handlers; `Result<T>` → HTTP via `ToHttpResult()` (`Validation→400`, `NotFound→404`, `Forbidden→403 generic`, `Conflict→409`).
- **Tenant**: Every write persists `tenantId` from `TenantContext`; every read filters by it before fetch.
- **Outbox**: Same transaction (`unitOfWork.SaveChangesAsync(ct)` dispatches domain events + stages integration events via `IOutboxWriter`).
- **Audit**: Append-only audit entry per write (actor, action, resource type/id, tenant, correlationId) via Audit BC topic.
