# Contract: API Health & Platform Health

**Feature**: 002-foundation-architecture | **Date**: 2026-08-31

## Health Endpoints (Standard ServiceDefaults)

Every service (Api + any future module hosts + Web BFF if applicable) exposes the health endpoints wired by `AddServiceDefaults()`.

### GET /health

- **Status**: 200 `Healthy`, 503 `Degraded`/`Unhealthy` (standard `HealthChecks` format)
- **Auth**: none (infrastructure probe)
- **Response** (example):
  ```json
  {
    "status": "Healthy",
    "checks": [
      { "name": "postgres", "status": "Healthy" },
      { "name": "rabbitmq", "status": "Healthy" },
      { "name": "redis", "status": "Healthy" }
    ]
  }
  ```

### GET /alive

- Liveness probe; 200 when the process is running, regardless of downstream dependencies.
- Response body optional; status code is the contract.

## Platform Health Query (`GetPlatformHealth`)

Vertical slice `src/Api/Features/GetPlatformHealth/`:

- **Handler**: `GetPlatformHealthQuery : IQuery<Result<PlatformHealthResponse>>`
- **Endpoint**: `GET /api/platform/health` (`IEndpoint` mapping) — uses `ISender.SendAsync(query)`
- **Auth**: none in dev; `Authorize` in non-dev (returns 401 without valid token from external discovery)
- **Success response** — `200 application/json`:
  ```json
  {
    "modules": [
      { "name": "Identity", "status": "Healthy", "dbReachable": true, "outboxBacklog": 0 }
    ],
    "identity": {
      "reachable": true,
      "discoveryEndpoint": "http://oroidentityserver/.well-known/openid-configuration",
      "latencyMs": 12,
      "error": null
    },
    "infra": {
      "postgres": { "status": "Healthy", "endpoint": "postgres:5432" },
      "rabbitmq": { "status": "Healthy", "endpoint": "rabbitmq:5672" },
      "redis":    { "status": "Healthy", "endpoint": "redis:6379" }
    }
  }
  ```
- **Identity unreachable** — `200` with `identity.reachable == false` and `error` populated (never crashes the caller):
  ```json
  { "identity": { "reachable": false, "discoveryEndpoint": "...", "error": "Discovery fetch failed: connection refused" } }
  ```
- **Error envelope**: Via `Result → HTTP` and `GlobalExceptionHandler` — `ProblemDetails` (`type`, `title`, `status`, `detail`) on failure.

## Validation

- `GET /health` and `GET /alive` respond <1 s on every service after `aspire run`.
- `GET /api/platform/health` includes all three sections (`modules`, `identity`, `infra`) and never throws when identity is down (see edge case in spec).
