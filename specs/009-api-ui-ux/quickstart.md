# Quickstart: API, UI and User Experience

**Feature**: 009-api-ui-ux | **Stack**: `Api` .NET 10 `IEndpoint` + `Web` Angular 22 + `@ngrx/signals` + `oroidentityserver` Podman | **Skills**: `minimal-ui-design-system` (`references/tokens.md`/`components.md`/`layout.md`), `ngrx-signal-store`

## Prereqs

```bash
dotnet --version # 10.0.400
node -v && pnpm -v
aspire workload update # Aspire 13.5
podman ps # oroidentityserver image localhost/oroidentityserver:latest
```

## Setup

```bash
git clone <orokanban> && cd OroKanban
dotnet build OroKanban.slnx
pnpm --dir src/Web install
```

## Run (Aspire)

```bash
aspire run --project OroKanban.AppHost
# Aspire Dashboard → api https://localhost:5001, web http://localhost:4200, identity https://localhost:5086
# Login vía oroidentityserver (seed admin admin/admin@oroclash.local)
```

Sin Aspire:

```bash
dotnet run --project src/Api --urls https://localhost:5001
pnpm --dir src/Web start -- --port 4200 # proxy a api via proxy.conf.json
```

## Validación manual (5 min)

1. **Contratos** — `GET /api/work-items?page=1&pageSize=10` → envelope `{items,total,page,pageSize}` + `Link`. `PUT` stale `version=4` vs `5` → `409 ProblemDetails` con `currentVersion`. `GET ?filter=bad` → `400`.
2. **Role nav** — Login `Contributor` → no `Team Tasks`/`Audit`/`Administration`; `GET /api/audit/entries` → `403`. Login `Manager` → sí los ve y `GET` → `200` subtree.
3. **Dashboard** — Manager A (OU-A 2 overdue) vs Manager B (OU-B 0) → KPIs `Overdue` difieren, `My Team`/`My SubManagers` reflejan profundidad ilimitada.
4. **Kanban → Detail** — Drag `In Progress→Blocked` → `PUT` ok, history entra; abrir `Work Item Detail` → progreso `66%` + `Why?` expande `subtasks 2/3`.
5. **Design** — Inspeccionar `kpi-card` → `shadow 0 8px 24px` Tier 2; `search-bar` → sin shadow Tier 1; `active nav pill` → `14px` + shadow; todo `Inter` + `8px` grid.
6. **Concurrency UX** — Editar mismo work item en 2 tabs, segunda guarda stale → toast `409` + `Reload`, ediciones preservadas.
7. **Tenant** — `Search` como tenant X nunca trae rows de tenant Y.

## Contratos

Ver `contracts/api-contracts.md` (envelope, ProblemDetails, ETag), `navigation-and-access.md` (16 rutas + matriz roles), `pages-spec.md` (12+4 páginas), `design-system.md` (tokens Tier 0/1/2/N), `state-stores.md` (SignalStore per feature).

## Tests

```bash
# Api contratos
dotnet test --filter *Contract* # pagination, filter, sort, ProblemDetails, ETag 409

# Web stores (Vitest)
pnpm --dir src/Web test -- --run --include="**/*.store.spec.ts"

# E2E role/nav + Kanban
pnpm --dir src/Web exec playwright test # o ng e2e si configurado
```

Todo verde → `SC-001…SC-008` cumplidos.

## Troubleshooting

- `401` → `access_token` expirado, relogin OIDC.
- `409` sin `currentVersion` → `RowVersion` no mapeado a `ETag` en handler.
- Nav muestra todo a Contributor → `RoleGuard` no lee `roles` del token.
- Card sin sombra donde debe haberla → revisar `tokens.scss` Tier 2 vs Tier 1.

