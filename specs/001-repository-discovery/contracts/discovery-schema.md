# Contract: Discovery Document Schema

**Feature**: 001-repository-discovery | **Date**: 2026-08-31

This "contract" defines the required structure of the discovery document produced at `draft/discovery/000-repository-catalog.md`. It is the interface between this documentary feature and its consumers (SPEC-001 architecture tests, later spec planners, reviewers).

## Document Header

```markdown
# Repository Catalog — OroKanban Discovery (SPEC-000)
**Date**: YYYY-MM-DD
**Commit**: <short SHA or "working tree">
**Author**: <actor>
**Spec**: specs/001-repository-discovery/spec.md
**Constitution**: .specify/memory/constitution.md v1.2.0
```

## Required Sections (in order)

### 1. draft/* Catalog

Table with columns: `Path | Status | Kind | Summary | Capabilities | Config Surface | Notes`

MUST include at minimum:

| Path | Kind |
|------|------|
| `draft/libraries/buildingblocks.md` | DraftDoc |
| `draft/oroidentityserver-specification.md` | DraftDoc |
| `draft/refined-specifications.md` | DraftDoc |

Each DraftDoc row MUST cite purpose + reusable primitives/capabilities (per FR-002).

### 2. Skills Catalog

Table with columns: `Skill | Path | Mandate | Principle | Scope | Status`

MUST flag the four PRIMARY skills (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) and list all other installed skills as SUPPLEMENTARY (per FR-003).

### 3. Solution Catalog

Table with columns: `Artifact | Status | Target / Version | Notes`

Artifacts: `OroKanban.slnx`, each `src/BuildingBlocks/*` project, `Directory.Build.props`, `Directory.Packages.props`, `global.json`.

### 4. Orchestration & Infrastructure Catalog

Table with columns: `Artifact | Status | Declared Resources | Config Pattern | Notes`

Artifacts: `OroKanban.AppHost/AppHost.cs`, `aspire.config.json` (may be NOT FOUND).

MUST record external `oroidentityserver` reference or its absence as a gap.

### 5. Cross-Cutting State

Table with columns: `Category | Status | Evidence | Notes`

Categories: `Identity integration | Persistence | UI framework | Testing | CI/CD`

Each category MUST have an explicit status; `NOT PRESENT` is a valid and required value when absent.

### 6. Capability Matrix

Table with columns: `Needed Capability | Provided by draft/* | Provided by Code/Skills | Gap? | ADR | Blocked Specs`

Seed rows (at minimum):

| Needed Capability |
|---|
| CQRS dispatch without MediatR (`ISender`, pipeline behaviors) |
| EventBus over RabbitMQ without MassTransit (topic exchange, outbox) |
| Domain primitives (`AggregateRoot`, `StronglyTypedId`, `Specification<T>`, `Result`) |
| `IEndpoint` + `Result → HTTP` + `GlobalExceptionHandler` |
| `AppDbContextBase` + `EfRepository` + transactional outbox |
| ServiceDefaults (OTel, health checks, HTTP resilience) + Serilog |
| External oroidentityserver via OIDC discovery + client registration |
| Prohibited-dependency guards (no MediatR/MassTransit/AutoMapper, no cross-module internals) |

Additional rows may be added for capabilities from refined specifications Part 0.

### 7. ADR Queue

Table with columns: `ADR | Problem Statement | Affected Specs | Owner | Priority | Source Entry`

Every `Gap? == Yes` row in the capability matrix MUST have a corresponding ADR row. Additional gaps from catalog entries (e.g., `BROKEN REFERENCE`) also appear here.

## Validation Rules

- The document MUST be valid Markdown and render without broken tables on GitHub preview.
- Every table MUST have a header separator row (`|---|---|`).
- Every `Gap? == Yes` row MUST have a non-empty `ADR` cell linking to the ADR Queue row.
- Status values MUST be exactly one of: `FOUND`, `NOT FOUND`, `INCOMPLETE`, `BROKEN REFERENCE`, `NOT PRESENT` (no synonyms).
- File paths MUST be repo-relative with forward slashes.

## Consumer Protocol

- **SPEC-001 architecture tests** consume sections 3, 6, and 7 to derive prohibited-dependency checks.
- **Later specs (001-014)** consume section 6 to cite covering primitives and section 7 to know which ADRs block them.
- **Reviewers** verify sections 1-5 for completeness (SC-001) and section 6 for the three-question procedure (SC-002).
