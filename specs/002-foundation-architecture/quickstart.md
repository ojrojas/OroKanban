# Quickstart: Foundation and Architecture Validation

**Feature**: 002-foundation-architecture | **Date**: 2026-08-31

## Prerequisites

- .NET 10 SDK 10.0.400 (`dotnet --version` from `global.json`)
- Podman (for `oroidentityserver` and Postgres/RabbitMQ/Redis containers)
- Node.js + `npx @angular/cli@latest` / `ng` (for `src/Web` scaffolding per FR-010)
- `oroidentityserver` image/container reachable (external, per constitution II)

## Scaffolding Log (FR-010 — record exact commands)

Record the commands used — they are the reproducibility contract for FR-010:

```bash
# .NET modules (repeat per module)
dotnet new classlib -n Identity.Domain -o src/Modules/Identity/Identity.Domain -f net10.0
dotnet new classlib -n Identity.Application -o src/Modules/Identity/Identity.Application -f net10.0
dotnet new classlib -n Identity.Infrastructure -o src/Modules/Identity/Identity.Infrastructure -f net10.0
dotnet new classlib -n Identity.Contracts -o src/Modules/Identity/Identity.Contracts -f net10.0
# (repeat for Organization, Projects, Metrics, Documents, AiProcessing, Search, Audit, Notifications)

# Composition API (exactly one scaffold)
dotnet new webapi -n Api -o src/Api -f net10.0

# Angular frontend (exactly one scaffold)
npx @angular/cli@latest new orokanban-web --directory src/Web --routing --style=scss --skip-git --package-manager npm
# (then adapt src/Web to minimal-ui-design-system tokens + ngrx-signal-store per SPEC-009)

# AppHost augmentation (Aspire SDK already present; add resources in AppHost.cs — no scaffold needed beyond `dotnet new aspire` patterns if desired)
dotnet --version > docs/scaffolding-log.md
npx @angular/cli@latest version >> docs/scaffolding-log.md
```

Commit the log (`docs/scaffolding-log.md` or commit-message body) so the scaffolding is auditable.

## Setup

```bash
dotnet restore OroKanban.slnx
dotnet build OroKanban.slnx -warnaserror
# Expected: 0 warnings on analyzer-enabled projects (SC-001). All 9 modules × 4 layers + Api + tests/Architecture should compile.
```

Add each new project to the solution (if not auto-added by the template):

```bash
dotnet sln OroKanban.slnx add src/Modules/Identity/Identity.Domain/Identity.Domain.csproj
# (repeat for each new csproj; likewise for src/Api/Api.csproj, src/Web not added to slnx, tests/Architecture)
```

## Run

```bash
# Preferred: Aspire run (or `aspire run` if Aspire CLI installed)
dotnet run --project OroKanban.AppHost/OroKanban.AppHost.csproj
# or: aspire run
# Expected within 2 min (SC-002): Aspire dashboard shows Postgres, RabbitMQ, Redis, oroidentityserver (external), api, web — each Healthy.
# Browse the dashboard URL printed in console.
```

## Verify

### 1. Solution structure

```bash
dotnet sln OroKanban.slnx list
# Must list: BuildingBlocks (6), Modules (36), Api, Architecture tests; no broken references

ls src/Modules/Identity/Identity.Domain src/Modules/Identity/Identity.Application \
   src/Modules/Identity/Identity.Infrastructure src/Modules/Identity/Identity.Contracts
# Each module has Domain/Application/Infrastructure/Contracts

# No cross-module Infrastructure refs (should produce no output):
grep -r "Modules\..*\.Infrastructure" src/Modules --include="*.csproj" | grep -v "Self"
```

### 2. Persistence convention

```bash
grep -r "AppDbContextBase" src/Modules --include="*.cs" | wc -l
# Expected: one per module Infrastructure (9)

grep -r "OutboxEntityTypeConfiguration" src/Modules --include="*.cs" | wc -l
# Expected: one per DbContext

grep -r "RowVersion\|ConcurrencyCheck" src/Modules --include="*.cs" | wc -l
# Expected: >= 1 (base aggregate row-version)

grep -r "Npgsql" src --include="*.csproj" | wc -l
# Expected: at least Api + each Infrastructure references Npgsql
```

### 3. ServiceDefaults + health

```bash
curl -s http://localhost:5000/health | jq .   # port from AppHost/dashboard
curl -s http://localhost:5000/alive  | head
curl -s http://localhost:5000/api/platform/health | jq .  # GetPlatformHealth (see contracts/api-health-contract.md)
# Expected: /health with postgres/rabbitmq/redis checks, /api/platform/health with modules+identity+infra sections; Serilog structured logs on stdout.
```

### 4. Identity fail-closed

```bash
Identity__Authority="" dotnet run --project src/Api -- --urls http://localhost:5099 2>&1 | head -n 20
# Expected: startup fails within 5 s with "Identity__Authority is required" (SC-005); no unauthenticated fallback.

# With valid Authority (external oroidentityserver running):
curl -s http://localhost:5080/.well-known/openid-configuration | jq .issuer
# Expected: issuer URL; confirms contracts/identity-config-contract.md discovery flow.
```

### 5. Architecture guard

```bash
dotnet test tests/Architecture -v minimal
# Expected (SC-004): PASS on clean repo; after deliberately adding
#   <PackageReference Include="MediatR" />
# to any module project, the suite FAILs within 10 s naming the project.
```

### 6. Build zero warnings

```bash
dotnet build OroKanban.slnx -warnaserror -v minimal 2>&1 | grep -i warning | wc -l
# Expected: 0
```

### 7. Seed (dev-only, optional)

```bash
# In development only, after identity is reachable:
curl -X POST http://localhost:5000/api/dev/seed -H "Content-Type: application/json" -d '{}'
# Expected: Result success; OroIdentityServer now has the demo tenant/org/users (verify via {authority}/.well-known/openid-configuration + admin UI).
# No credentials stored locally.
```

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| AppHost shows no Postgres/RabbitMQ/Redis | AppHost.cs still bare `Build().Run()` | Apply ADR-001 declarations in `research.md` Decision 2 |
| `/health` missing | `AddServiceDefaults()` not called in `Program.cs` | Wire ServiceDefaults in Api's `Program.cs` per `data-model.md` (Api composition) |
| Architecture test reports cross-module Infrastructure ref | A module added a `ProjectReference` to another module's Infrastructure | Remove the reference; communicate via `Contracts` + EventBus instead |
| `ng new` fails | Angular CLI not installed or Node version old | `npm i -g @angular/cli@latest`; check `ng version` |
| `dotnet new webapi` creates WeatherForecast | Template default sample | Delete sample files after scaffolding per `research.md` Decision 1 |
| Identity discovery 404 | Authority URL wrong or oroidentityserver container not running | `podman ps` — start the external server; verify `GET {authority}/.well-known/openid-configuration` independently |

## What is NOT validated here

- Rich domain logic per module (empty aggregates are expected at foundation stage — domain depth arrives in later specs).
- Frontend UI behavior beyond Angular scaffold + health wiring (minimal-ui-design-system/ngrx adaptation is SPEC-009).
- CI pipeline (ADR-003/006) — local `dotnet build` + `dotnet test tests/Architecture` is sufficient for this phase.
