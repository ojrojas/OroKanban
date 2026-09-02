# Contract: API Contracts First

**Module**: `Api` (BC-10 Platform) | **Base**: `/api` | **Auth**: `Authorization: Bearer <access_token>` (`oroidentityserver` OIDC) | **Pattern**: `IEndpoint` + `Result→HTTP` (`BuildingBlocks.ServiceDefaults.Endpoints.ResultExtensions`) + `GlobalExceptionHandler`

## Envelope

```json
// GET /api/{resource}?page=1&pageSize=20&q=&filter=&sort=&sortDir=
{
  "items": [ { /* DTO */ } ],
  "total": 25,
  "page": 1,
  "pageSize": 20
}
// Header: Link: <http://host/api/work-items?page=2&pageSize=20>; rel="next"
```

`Dto` nunca es `AggregateRoot` interno. Mapeo manual en handler.

## ProblemDetails

```json
// 400 Validation
{
  "type": "https://httpstatuses.io/400",
  "title": "Validation failed",
  "detail": "filter 'unknownField' is invalid",
  "status": 400,
  "code": "Validation.FilterUnknown"
}
// 409 Concurrency
{
  "type": "https://httpstatuses.io/409",
  "title": "Concurrency conflict",
  "detail": "Version 4 is stale, current is 5",
  "status": 409,
  "code": "Concurrency.StaleVersion",
  "currentVersion": "5"
}
// 403 Forbidden (centralizado)
{
  "type": "https://httpstatuses.io/403",
  "title": "Forbidden",
  "detail": "Missing permission audit.read",
  "status": 403,
  "code": "Auth.Forbidden"
}
```

`ErrorType.Validation→400`, `NotFound→404`, `Conflict→409`, `Forbidden→403`, `Unauthorized→401`.

## Concurrencia

- `GET` retorna DTO con `version` (`RowVersion` base64) + header `ETag: W/"<version>"`
- `PUT/PATCH` requiere `If-Match: W/"<version>"` o campo `version` en body
- Stale → `409`/`412` con `ProblemDetails` + `currentVersion`, sin overwrite

## Filtrado / Sorting / Search

`?q=shipped` search tenant-filtered, `?filter=status:Blocked&filter=overdue:true`, `?sort=createdAt&sortDir=desc`, `?page=&pageSize=` (1..100). Siempre delegado a API (`Specification<T>` + `IManagementHierarchy` subtree + `tenant_id`), nunca filtrado cliente de dump completo.

## Endpoints base ya existentes (reusados, no nuevos)

- `GET /api/projects`, `GET /api/work-items`, `GET /api/documents`, `GET /api/audit/entries`, `GET /api/notifications` — todos ya con envelope + `ProblemDetails`.

## Nuevos para UI (si faltan, se añaden como slices)

- `GET /api/dashboard/kpis` → `DashboardKPI[]` (subtree-filtered, ver `navigation-and-access.md`)
- `GET /api/work-items/:id/detail` → `WorkItemDetailAggregate` (incluye `progressExplanation`)
- `GET /api/search?q=&type=` → `SearchResult[]` tenant-aware

**Tenant/Org:** todo handler hace `Where(tenant_id == ctx.tenant_id)` + `IManagementHierarchy.GetSubtreeIds(currentUserId)` antes del fetch.

## Testing contrato

`xUnit` + `TestHost`: `GET ?page=2&pageSize=10` verifica `total/page/Link`; `PUT` stale verifica `409` + `currentVersion`; `GET ?filter=bad` verifica `400 ProblemDetails.code == Validation.*`.
