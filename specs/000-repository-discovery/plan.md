# Implementation Plan: Repository Discovery

**Branch**: `001-repository-discovery` | **Date**: 2026-08-31 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-repository-discovery/spec.md`

## Summary

Convert the constitution's Repository Discovery Gate (Principles I, XXI, XXII) into an executable first deliverable: a catalog at `draft/discovery/000-repository-catalog.md` that inventories `draft/*` rule bases, `.agents/skills` mandates, solution structure, Aspire orchestration, and cross-cutting state, plus a capability matrix and ADR queue. Success unblocks all later specs by making the constitution concrete.

## Technical Context

**Language/Version**: Markdown + inspection tooling on .NET 10 (SDK 10.0.400 per `global.json`); no new runtime language

**Primary Dependencies**: `draft/libraries/buildingblocks.md` (BuildingBlocks canon), `draft/oroidentityserver-specification.md` (OIDC/OAuth2 canon), workspace skills under `.agents/skills/` (dotnet-ai, ddd-project-planner, minimal-ui-design-system, ngrx-signal-store as constitution-mandated, plus supplementary aspire-*, dotnet-inspect, playwright-cli), `OroKanban.slnx` + `Directory.*.props` + `global.json`, `OroKanban.AppHost/AppHost.cs`

**Storage**: N/A — output is a single Markdown document at `draft/discovery/000-repository-catalog.md`; source artifacts are filesystem reads only

**Testing**: Documentary verification (no unit/integration tests). Validation is manual review against FR-002..FR-009 and success criteria SC-001..SC-005; checklist at `checklists/requirements.md`

**Target Platform**: Local development filesystem (Linux, Podman available for oroidentityserver reference)

**Project Type**: Documentation / discovery gate

**Performance Goals**: N/A — single document production completes in <1 minute on any checkout

**Constraints**: MUST NOT introduce new dependencies (Principle I three-question gate); MUST NOT duplicate identity server; MUST NOT generate runtime code, migrations, or service launches

**Scale/Scope**: One output document cataloging ~15 source artifacts + capability matrix (~10-15 rows) + ADR queue (0-N candidates)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- [x] **Principle I — Existing Assets Authoritative**: Discovery reuses `draft/*` and skills; proposes no new libraries. Three-question procedure is documented as FR-008.
- [x] **Principle II — oroidentityserver Mandatory**: Discovery catalogs the external identity integration point (AppHost external reference, OIDC discovery endpoint) without duplicating it.
- [x] **Principle III — .NET 10**: Inspection reads `global.json` SDK 10.0.400; no legacy target introduced.
- [x] **Principle IV — Aspire Orchestrator**: Catalogs `OroKanban.AppHost/AppHost.cs` as the composition source; does not reinvent orchestration.
- [x] **Principle XXI — TDD+DDD+Vertical Slices via draft/* canons**: BuildingBlocks and oroidentityserver specs are treated as the authoritative rule base; discovery output makes them citeable for later specs.
- [x] **Principle XXII — Workspace Skills Govern Design**: All installed skills are cataloged; the four mandated skills are flagged as primary rule bases.
- [x] **Gate J — Repository Discovery Gate (§Development Lifecycle)**: This feature IS the gate. No production feature may start until it completes; violation would be a constitutional failure (ERROR if skipped).
- [x] **Modular Architecture / Domain Rules**: Not applicable to this documentary feature — no new modules or domain logic introduced; discovery only inspects.
- [x] **Security / Audit / Observability**: Discovery document contains no secrets; it records configuration surface without values.

**Result: PASS — no violations, no complexity exceptions needed.**

## Project Structure

### Documentation (this feature)

```text
specs/001-repository-discovery/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (discovery document schema)
│   └── discovery-schema.md
└── checklists/
    └── requirements.md  # Spec quality checklist (already created by /speckit.specify)
```

### Source Code (repository root)

```text
draft/
├── libraries/buildingblocks.md          # inspected (read-only)
├── oroidentityserver-specification.md   # inspected (read-only)
├── refined-specifications.md            # inspected (read-only, traceability source)
└── discovery/
    └── 000-repository-catalog.md        # PRODUCED by this feature (only write target)

.agents/skills/                          # inspected (read-only)
OroKanban.slnx                           # inspected (read-only)
OroKanban.AppHost/AppHost.cs             # inspected (read-only)
src/BuildingBlocks/*                     # inspected (read-only)
Directory.Build.props / Directory.Packages.props / global.json  # inspected (read-only)

specs/001-repository-discovery/          # spec + plan artifacts (this feature)

# No new src/ code, no new tests, no new projects for this feature.
# Runtime implementation of discovered gaps happens in later specs (001-014).
```

**Structure Decision**: Documentary-only feature. The sole write target is `draft/discovery/000-repository-catalog.md`. All other paths are read-only inspections. No new projects, no migrations, no service configuration changes.

## Complexity Tracking

> No violations — table not needed.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
