# Contract: Permission Catalog (Seeded, Not Hard-Coded)

**Feature**: 003-identity-access-organization | **Date**: 2026-08-31
**Published by**: `Identity.Domain` (Permission value objects) | **Implemented by**: `Identity.Infrastructure` (seeder + repository), consumed via `Identity.Contracts`

## PermissionCode (ValueObject)

```csharp
namespace Identity.Domain.ValueObjects;

public sealed class PermissionCode : ValueObject
{
    public string Value { get; } // e.g., "project.read", "workitem.assign", "document.approve"
    protected override IEnumerable<object?> GetEqualityComponents() { yield return Value; }
}
```

## Permission (Catalog Entry, seeded)

```csharp
public sealed class Permission : Entity<Guid>
{
    public PermissionCode Code { get; }
    public string Description { get; }
    public string Category { get; } // project | workitem | document | ai | audit | organization
}
```

Seeded via `Identity.Infrastructure/Seed/PermissionSeederHostedService` on first run (reads `PermissionsSeed.json` or a code list in `PermissionCatalogSeed.cs`). Not hard-coded in the evaluator.

### Seeded permissions (R2)

| Category | Codes |
|----------|-------|
| project | `project.read`, `project.create`, `project.update`, `project.delete` |
| workitem | `workitem.read`, `workitem.create`, `workitem.assign`, `workitem.update`, `workitem.complete` |
| document | `document.read`, `document.upload`, `document.classify`, `document.version`, `document.approve` |
| ai | `ai.execute`, `ai.review`, `ai.approve` |
| audit | `audit.read` |
| organization | `organization.manage` |

Additional permissions are added by inserting a `Permission` row — no evaluator code change.

## Role → Permission Map (seeded)

```csharp
public sealed class RolePermission
{
    public string Role { get; } // e.g., "Manager"
    public PermissionCode Permission { get; }
}
```

Seeded roles (10 initial, R2): `RootManager`, `Manager`, `Supervisor`, `Contributor`, `Reviewer`, `Auditor`, `DocumentManager`, `ProjectManager`, `AIReviewer`, `Administrator`.

Seed mapping is the source of truth for `IPermissionCatalog.HasPermission(roles, permission)`; roles themselves live in OroIdentityServer (Principle II) — the app stores only the map.

Example seed (illustrative):

| Role | Permissions |
|------|-------------|
| RootManager | all |
| Manager | `project.read/create/update`, `workitem.read/create/assign/update/complete`, `document.read/upload`, `organization.manage` |
| Contributor | `project.read`, `workitem.read/create/update`, `document.read/upload` |
| Auditor | `audit.read`, `project.read`, `workitem.read`, `document.read` |
| ... | ... |

Full seed lives in `Identity.Infrastructure/Seed/RolePermissionSeed.cs` and is extensible by adding rows.

## IPermissionCatalog (consumed by evaluator)

```csharp
namespace Identity.Contracts;

public interface IPermissionCatalog
{
    Task<bool> HasPermissionAsync(IReadOnlyList<string> roles, string permission, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(string role, CancellationToken ct);
}
```

Implemented in `Identity.Infrastructure` via `EfRepository<Permission>` + `RolePermission` table lookups; cacheable in Redis with a 10-min TTL (invalidated only on catalog change).

## Validation

- Seeded catalog contains all permission codes listed above — `ListAsync(new AllPermissionsSpecification())` count matches seed count.
- `HasPermissionAsync(["Contributor"], "workitem.assign")` → true; `HasPermissionAsync(["Auditor"], "workitem.assign")` → false.
- Adding a new `Permission("project.archive", ...)` row and a `RolePermission("Manager", "project.archive")` row makes `HasPermissionAsync(["Manager"], "project.archive")` return true without redeploying the evaluator.
