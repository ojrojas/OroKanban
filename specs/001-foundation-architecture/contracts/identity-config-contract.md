# Contract: Identity Configuration (External oroidentityserver)

**Feature**: 002-foundation-architecture | **Date**: 2026-08-31

## Environment-Only Configuration

No secret or endpoint is hard-coded. All identity settings are supplied via environment / Aspire configuration per constitution §Configuration and spec FR-005. Missing settings cause fail-fast (never silent fallback).

| Key | Example | Required | Notes |
|-----|---------|----------|-------|
| `Identity__Authority` | `http://oroidentityserver:5080` or `http://localhost:5080` | Yes | Base URL of the external server; used as `options.Authority` and to derive `/.well-known/openid-configuration` |
| `Identity__Audience` | `orokanban-api` | Yes | Expected `aud` claim |
| `Identity__ClientId` | `orokanban-api` | Yes (for `authorization_code` flow) | Registered via `POST /api/applications` on the external server |
| `Identity__ClientSecret` | `***` | Yes (for confidential client) | Never logged; via User Secrets in dev, secret store in prod |
| `Identity__Scopes` | `openid profile email offline_access` | No (default as shown) | Space-separated |
| `ConnectionStrings__oroidentityserver__discovery` | `http://oroidentityserver/.well-known/openid-configuration` | No — derived from Authority | Overridable for tests |

## Discovery Flow

1. Service derives `discoveryEndpoint = {Authority}/.well-known/openid-configuration`.
2. On startup (or first token validation), `AddJwtBearer` / `AddOpenIdConnect` fetches the discovery document; `issuer`, `jwks_uri`, `authorization_endpoint`, `token_endpoint` are used for validation.
3. The handler extracts claims from `/connect/userinfo`: `sub`, `email`, `name`, `roles`, `tenant_id` (mapped from `draft/oroidentityserver-specification.md` claim `tenant_id`).
4. `tenant_id` is propagated as tenant context (`IClaimsTransformation` or middleware stores `TenantId` in `HttpContext.Items` / scoped `TenantContext`).

## Fail-Closed Behavior

- If any of `Authority`/`Audience`/`ClientId` is absent or whitespace, the host fails on `builder.Build()` / first `ValidateOnStart()` with a `OptionsValidationException` whose message names the missing key, e.g. `Identity__Authority is required but was not configured`.
- Logging must NOT emit secret values (ClientSecret, SymmetricSecurityKey if any).
- `SeedDevelopmentData` (dev-only) uses the same settings to call admin APIs `POST /api/tenants`, `/api/users`, `/api/users/{id}/roles` — it also fails with the same message when settings are absent.

## Client Registration (Out-of-band)

Not part of the OroKanban runtime, but part of the platform setup procedure documented in `docs/setup-identity.md`:

```bash
# once the external server is running, register the Api client
curl -X POST http://oroidentityserver:5080/api/applications \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "orokanban-api",
    "clientSecret": "<secret>",
    "displayName": "OroKanban API",
    "consentType": "Implicit",
    "grantTypes": ["authorization_code", "refresh_token"],
    "redirectUris": ["http://localhost:5000/callback"],
    "permissions": ["openid", "profile", "email", "offline_access"]
  }'
# or via the Blazor admin UI at {authority}/Applications → Create
```

## Aspire Wiring

`OroKanban.AppHost/AppHost.cs` (scaffolded via `dotnet new aspire-*` pattern):

```csharp
var oroidentity = builder.AddContainer("oroidentityserver", "ghcr.io/ojrojas/oroidentityserver", "latest")
  .WithEndpoint("http", targetPort: 5080, name: "http")
  .WithEnvironment("ASPNETCORE_URLS", "http://+:5080");

builder.AddProject<Projects.Api>("api")
  .WithReference(postgres).WaitFor(postgres)
  .WithReference(rabbitmq).WaitFor(rabbitmq)
  .WithReference(redis).WaitFor(redis)
  .WithReference(oroidentity).WaitFor(oroidentity)
  .WithEnvironment("Identity__Authority", oroidentity.GetEndpoint("http"));
```

The `Web` (Angular) dev server is similarly registered via `AddNpmApp`/`AddViteApp` or `AddContainer` per Aspire's frontend support.

## Validation

- `GET {Authority}/.well-known/openid-configuration` returns OpenIddict metadata (issuer, jwks_uri, etc.).
- `POST /connect/token` with `authorization_code` + registered client returns a JWT whose `aud` and `tenant_id` are consumable by the Api's bearer handler.
- Missing `Identity__Authority` on startup yields the fail-fast message within 5 s (SC-005).
