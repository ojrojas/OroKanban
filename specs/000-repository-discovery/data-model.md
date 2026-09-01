# Data Model: Repository Discovery

**Feature**: 001-repository-discovery | **Date**: 2026-08-31

This feature is documentary. The "data model" is the structure of the discovery document itself and the conceptual entities it catalogs. No runtime persistence is introduced.

## Entities

### 1. Discovery Catalog Entry

Represents one inspected artifact.

| Field | Type | Validation | Notes |
|-------|------|------------|-------|
| `path` | string (relative) | MUST be non-empty, normalized with forward slashes | e.g., `draft/libraries/buildingblocks.md` |
| `status` | enum `CatalogStatus` | One of `FOUND` \| `NOT FOUND` \| `INCOMPLETE` \| `BROKEN REFERENCE` | `INCOMPLETE` = file exists but expected content (e.g., SKILL.md) missing |
| `kind` | enum `CatalogKind` | One of `DraftDoc` \| `Skill` \| `Solution` \| `Project` \| `Config` \| `AppHost` \| `CrossCutting` | Groups entries for readability |
| `summary` | string | 1-3 sentences | Purpose + reusable capabilities |
| `capabilities` | string[] | Optional, empty if INCOMPLETE/NOT FOUND | Primitives, endpoints, flows, config knobs |
| `configSurface` | string | Optional | Connection strings, env vars, or settings exposed |
| `notes` | string | Optional | Edge-case handling, citations (file:line) |

**State transitions**: None — entries are terminal records. Corrections produce a new discovery document revision (document versioning per constitution §Versioning, applied to this deliverable only).

### 2. Skill Mandate Record

| Field | Type | Validation |
|-------|------|------------|
| `skillName` | string | Directory name under `.agents/skills/` |
| `skillPath` | string | e.g., `.agents/skills/dotnet-ai` |
| `mandateClass` | enum `MandateClass` | `PRIMARY` (four constitution-mandated) \| `SUPPLEMENTARY` |
| `principleRef` | string | e.g., `XXII/dotnet-ai`, `XXII/minimal-ui-design-system`, or `supplementary` |
| `description` | string | From skill's frontmatter / SKILL.md description |
| `mandatedScope` | string | Usage scope per constitution (e.g., "all AI/ML technology decisions") |

**Validation**: The four PRIMARY skills MUST all be present with `FOUND`; absence raises an ADR candidate.

### 3. Capability Matrix Row

| Field | Type | Validation |
|-------|------|------------|
| `neededCapability` | string | Derived from constitution principles / refined Specs Part 0 (e.g., "CQRS dispatch without MediatR") |
| `providedByDraft` | string | File ref + capability name, or `—` if not in draft/* |
| `providedByCode` | string | Existing project/skill ref, or `—` |
| `gap` | boolean | `true` iff neither column covers the need |
| `adrCandidateId` | string (nullable) | e.g., `ADR-002` if `gap == true` |
| `blockedSpecs` | string[] | Spec IDs blocked by the gap (e.g., `SPEC-002`, `SPEC-010`) |

### 4. ADR Candidate

| Field | Type | Validation |
|-------|------|------------|
| `adrId` | string | `ADR-000`…`ADR-0NN`, sequential from discovery |
| `problemStatement` | string | One sentence: what is missing/ambiguous |
| `affectedSpecs` | string[] | Spec IDs blocked or impacted |
| `suggestedOwner` | string | Role or person (e.g., "Platform architect") |
| `priority` | enum `Priority` | `P1` (blocks next sprint) \| `P2` \| `P3` |
| `sourceEntryPath` | string | Catalog entry path that revealed the gap |

## Relationships

```
Discovery Document
  ├── 1..* Discovery Catalog Entry
  │     └── 0..* Skill Mandate Record (for kind == Skill)
  ├── 0..* Capability Matrix Row
  │     └── 0..1 ADR Candidate (if gap)
  └── 0..* ADR Candidate (also directly from catalog entries)
```

## Validation Rules (from Requirements)

- FR-002: At least entries for `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md` MUST exist with status `FOUND` and non-empty `capabilities`.
- FR-003: At least the four PRIMARY skills MUST be cataloged; all other installed skills MUST also appear (no silent omission).
- FR-004: Entries for `OroKanban.slnx`, `global.json`, and each `src/BuildingBlocks/*` project MUST appear.
- FR-005: Entry for `OroKanban.AppHost/AppHost.cs` MUST appear; `aspire.config.json` MAY be `NOT FOUND`.
- FR-006: Cross-cutting categories (identity, persistence, UI, testing, CI/CD) MUST each have an entry, with explicit status.
- FR-007: Matrix MUST cover at least the constitution's required capabilities (BuildingBlocks primitives, oroidentityserver external integration, prohibited-dependency guards).
- FR-008: Three-question procedure MUST be described in the document so that FR-009 (architecture-test derivability) holds.
