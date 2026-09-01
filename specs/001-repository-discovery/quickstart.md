# Quickstart: Repository Discovery Validation

**Feature**: 001-repository-discovery | **Date**: 2026-08-31

This feature is documentary. Validation proves the discovery document was produced correctly and is consumable by later specs.

## Prerequisites

- Clean checkout of `OroKanban` at any commit after `specs/001-repository-discovery/spec.md` exists
- Read access to `draft/`, `.agents/skills/`, `OroKanban.slnx`, `OroKanban.AppHost/AppHost.cs`, `src/BuildingBlocks/*`, `Directory.*.props`, `global.json`
- No new dependencies, no service launches required

## Steps

### 1. Produce the discovery document

The implementation of this feature writes `draft/discovery/000-repository-catalog.md` per `contracts/discovery-schema.md`.

**Expected outcome**: File exists at `draft/discovery/000-repository-catalog.md`.

Verify:

```bash
ls -la draft/discovery/000-repository-catalog.md
head -n 20 draft/discovery/000-repository-catalog.md
# Header must show Date, Commit, Spec ref, Constitution v1.2.0
```

### 2. Verify all required sections are present

```bash
grep -n "^## \|^### " draft/discovery/000-repository-catalog.md
# Must list: draft/* Catalog, Skills Catalog, Solution Catalog,
#            Orchestration & Infrastructure Catalog, Cross-Cutting State,
#            Capability Matrix, ADR Queue
```

**Expected outcome**: All 7 sections from `contracts/discovery-schema.md` are present in order.

### 3. Verify every `draft/*` document is cataloged

```bash
grep -c "buildingblocks.md" draft/discovery/000-repository-catalog.md
grep -c "oroidentityserver-specification.md" draft/discovery/000-repository-catalog.md
# Each must be >= 1
```

**Expected outcome**: Both canonical rule bases appear with `FOUND` and a capability summary.

### 4. Verify skills catalog

```bash
grep -n "dotnet-ai\|ddd-project-planner\|minimal-ui-design-system\|ngrx-signal-store" draft/discovery/000-repository-catalog.md
# Each of the four PRIMARY skills must appear with Mandate == PRIMARY
grep -n "aspire" draft/discovery/000-repository-catalog.md
# Supplementary skills must also appear
```

**Expected outcome**: Four PRIMARY rows + supplementary rows; no skill directory silently omitted.

### 5. Verify "NOT PRESENT" handling

```bash
grep -n "NOT PRESENT\|NOT FOUND\|BROKEN REFERENCE\|INCOMPLETE" draft/discovery/000-repository-catalog.md
# At least cross-cutting gaps should show explicit statuses per FR-006
```

**Expected outcome**: Every absent capability has an explicit status — not a missing row.

### 6. Verify capability matrix and ADR queue

```bash
# Count matrix rows vs ADR rows
grep -c "Gap?" draft/discovery/000-repository-catalog.md
grep -c "ADR-" draft/discovery/000-repository-catalog.md
# Every Gap? == Yes row must have a corresponding ADR entry
```

**Expected outcome**: Capability matrix covers at least the 8 seed rows from `contracts/discovery-schema.md`; every gap row links to an ADR queue entry.

### 7. Verify architecture-test derivability (SC-004)

As a reviewer, draft 3 architecture tests from the discovery document alone:

1. "No project may reference MediatR/MassTransit/AutoMapper" — cite the matrix row that names the BuildingBlocks replacement.
2. "No module may reference another module's Internal/Infrastructure" — cite the solution catalog + matrix row for prohibited cross-module access.
3. "Every DbContext inherits `AppDbContextBase` and applies outbox configuration" — cite the BuildingBlocks catalog entry.

**Expected outcome**: All three tests can be written without re-inspecting source — discovery alone is sufficient.

### 8. Verify downstream citeability (SC-005)

Open `draft/refined-specifications.md` Part 0 and pick any capability claim; verify it can cite a discovery entry:

```bash
grep -n "BuildingBlocks" draft/discovery/000-repository-catalog.md | head
# Use these lines as citations in SPEC-001
```

**Expected outcome**: At least one discovery entry is citeable for each of the 8 seed capabilities.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `draft/discovery/` does not exist | Feature not yet implemented | Run the implementation task that writes the catalog |
| Missing skill rows | `.agents/skills` not scanned recursively | Re-run discovery scanning that directory |
| `NOT FOUND` for `aspire.config.json` | File genuinely absent — this is expected | Record as `NOT FOUND`, not an error; add gap ADR only if a capability depends on it |
| AppHost shows no external oroidentityserver | Bare `builder.Build().Run()` without external resource | Record as gap → ADR for identity integration approach |

## What is NOT validated here

- No unit/integration/E2E test suites are produced for this feature (it is documentary).
- Implementation of gaps (actual ADRs, AppHost wiring, module skeletons) happens in later specs (001-014) per the roadmap.
