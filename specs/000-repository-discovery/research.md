# Research: Repository Discovery

**Feature**: 001-repository-discovery | **Date**: 2026-08-31 | **Status**: Complete

No NEEDS CLARIFICATION markers were present in the spec or Technical Context. This document records the decisions that would otherwise be research outputs, so that Phase 1 artifacts have explicit rationale.

## Decision 1: Output location `draft/discovery/000-repository-catalog.md`

- **Decision**: Use `draft/discovery/000-repository-catalog.md` as the discovery document path; create `draft/discovery/` if absent.
- **Rationale**: Matches the spec's FR-001 and FR-007, preserves `draft/` as the single source for architecture canons (constitution Principle XXI), and mirrors the spec's own `SPEC-000` numbering so discovery precedes all later specs.
- **Alternatives considered**: `specs/001-.../discovery.md` (rejected — discovery is cross-cutting, not feature-scoped), `.specify/memory/...` (rejected — that location is reserved for constitution), repo root `DISCOVERY.md` (rejected — pollutes root).

## Decision 2: Catalog scope and inspection method

- **Decision**: Inspect via filesystem reads + manual summarization; no code generation, no service launches, no migrations.
- **Rationale**: Spec is documentary (Assumption: no runtime code required). Filesystem inspection is sufficient and keeps the feature compliant with the "no new dependencies" constraint.
- **Alternatives considered**: Automated script that parses `.csproj` XML and generates the catalog (rejected — over-engineered for a one-time gate; a hand-authored catalog with explicit citations is more reviewable and matches the "test is that SPEC-001 tests can be written from it" criterion). A lightweight script may be added later as a verification helper, but is not required for the gate.

## Decision 3: Handling of "NOT PRESENT" / gaps

- **Decision**: Every absent capability is recorded explicitly as `NOT PRESENT` / `NOT FOUND` / `REFERENCE BROKEN` / `INCOMPLETE` with a status enum, and each such row produces an ADR candidate.
- **Rationale**: Required by edge cases and FR-006/FR-007. Silent omission would violate the constitution's "gaps are ADR candidates, not improvisations" rule.
- **Alternatives considered**: Omit absent rows (rejected — hides gaps), fail the discovery workflow on any gap (rejected — discovery must complete even with gaps; gaps block later specs, not discovery itself).

## Decision 4: Skill mandate classification

- **Decision**: Flag the four constitution-mandated skills (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) as PRIMARY, and all other installed skills (`aspire`, `aspire-deployment`, `aspire-init`, `aspire-monitoring`, `aspire-orchestration`, `aspireify`, `dotnet-inspect`, `playwright-cli`) as SUPPLEMENTARY.
- **Rationale**: Directly implements Principle XXII and the spec's edge-case handling. Prevents later accidental reliance on a supplementary skill as if it were authoritative.
- **Alternatives considered**: Treat all skills equally (rejected — loses the constitutional priority signal).

## Decision 5: Capability matrix shape

- **Decision**: Matrix columns = Needed capability | Provided by `draft/*` (with file:line ref) | Provided by existing code/skills | Gap (with ADR candidate ID).
- **Rationale**: Satisfies FR-007 and makes the three-question decision procedure (FR-008) traceable per row. File:line references keep the matrix auditable.
- **Alternatives considered**: Simpler two-column "need → covered?" (rejected — loses traceability to the covering primitive).

## Decision 6: Discovery document schema vs. ad-hoc Markdown

- **Decision**: Define a lightweight schema for the discovery document (see `contracts/discovery-schema.md`) rather than free-form prose.
- **Rationale**: Makes SC-004 testable ("architecture tests can be authored from discovery") — a schema guarantees the sections and fields that tests will consume.
- **Alternatives considered**: No schema (rejected — would allow drift and make SC-004 unverifiable).

## Open Items

None. All unknowns resolved; no NEEDS CLARIFICATION remains.
