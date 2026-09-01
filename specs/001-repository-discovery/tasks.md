# Tasks: Repository Discovery

**Input**: Design documents from `/specs/001-repository-discovery/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/discovery-schema.md, quickstart.md
**Constitution**: v1.2.0 enforced — Principles I, XXI, XXII, Gate J

**Tests**: No automated tests for this documentary feature. Validation is manual review per `quickstart.md` and `checklists/requirements.md`.

**Organization**: Tasks grouped by user story to enable independent implementation and testing of each story's deliverable increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Prepare the output location and verify the source inventory exists

- [x] T001 Create discovery output directory `draft/discovery/` (ensure `draft/discovery/000-repository-catalog.md` can be written)
- [x] T002 Verify all source artifacts exist and are readable: `draft/libraries/buildingblocks.md`, `draft/oroidentityserver-specification.md`, `.agents/skills/*`, `OroKanban.slnx`, `OroKanban.AppHost/AppHost.cs`, `src/BuildingBlocks/*`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` under repository root

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Gather raw inventory that all user stories depend on — MUST complete before any story work

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T003 [P] Inventory draft/* documents by listing all files under `draft/` with path and byte size in `draft/discovery/000-repository-catalog.md` (draft, temporary section — will be replaced by FR-002 content)
- [x] T004 [P] Inventory installed skills by listing all directories under `.agents/skills/` with SKILL.md presence check in `draft/discovery/000-repository-catalog.md` (temporary section)
- [x] T005 [P] Inventory solution structure by parsing `OroKanban.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, and `src/BuildingBlocks/*` project files and recording findings in `draft/discovery/000-repository-catalog.md` (temporary section)
- [x] T006 Define final document outline per `specs/001-repository-discovery/contracts/discovery-schema.md` in `draft/discovery/000-repository-catalog.md` (replace temporary sections with the 7 required section headers in order: Header, draft/* Catalog, Skills Catalog, Solution Catalog, Orchestration Catalog, Cross-Cutting State, Capability Matrix, ADR Queue)

**Checkpoint**: Raw inventories collected, outline in place — story implementation can now begin

---

## Phase 3: User Story 1 - Architect performs mandatory discovery before any feature (Priority: P1) 🎯 MVP

**Goal**: Produce the complete catalog so every later feature can cite `draft/*` and skills instead of reinventing them

**Independent Test**: Run discovery on a clean checkout; `draft/discovery/000-repository-catalog.md` exists with all 7 sections populated. A reviewer can verify every `draft/*` document is cataloged with capabilities in under 5 minutes (SC-001). No runtime code involved.

### Implementation for User Story 1

- [x] T007 [P] [US1] Write **Header** section (Date, Commit SHA, Spec ref, Constitution v1.2.0) in `draft/discovery/000-repository-catalog.md` per contracts/discovery-schema.md
- [x] T008 [P] [US1] Write **1. draft/* Catalog** table (Path | Status | Kind | Summary | Capabilities | Config Surface | Notes) in `draft/discovery/000-repository-catalog.md` — must include `draft/libraries/buildingblocks.md` (BuildingBlocks primitives) and `draft/oroidentityserver-specification.md` (OIDC endpoints/flows/config knobs) with FR-002 detail
- [x] T009 [P] [US1] Write **2. Skills Catalog** table (Skill | Path | Mandate | Principle | Scope | Status) in `draft/discovery/000-repository-catalog.md` — flag 4 PRIMARY skills (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) vs SUPPLEMENTARY (aspire-*, dotnet-inspect, playwright-cli) per FR-003
- [x] T010 [P] [US1] Write **3. Solution Catalog** table (Artifact | Status | Target/Version | Notes) in `draft/discovery/000-repository-catalog.md` — cover `OroKanban.slnx`, each `src/BuildingBlocks/*` project, `Directory.Build.props`, `Directory.Packages.props`, `global.json` per FR-004
- [x] T011 [US1] Write **4. Orchestration & Infrastructure Catalog** table (Artifact | Status | Declared Resources | Config Pattern | Notes) in `draft/discovery/000-repository-catalog.md` — inspect `OroKanban.AppHost/AppHost.cs` and `aspire.config.json` (may be NOT FOUND), record external `oroidentityserver` reference or its absence as gap per FR-005
- [x] T012 [US1] Write **5. Cross-Cutting State** table (Category | Status | Evidence | Notes) in `draft/discovery/000-repository-catalog.md` — categories: Identity integration, Persistence, UI framework, Testing, CI/CD with explicit `FOUND`/`NOT PRESENT` per FR-006 (covers edge case: absent capabilities not silently omitted)
- [x] T013 [US1] Write **6. Capability Matrix** table (Needed Capability | Provided by draft/* | Provided by Code/Skills | Gap? | ADR | Blocked Specs) in `draft/discovery/000-repository-catalog.md` — seed at least the 8 rows from contracts/discovery-schema.md plus any From refined specifications Part 0 per FR-007
- [x] T014 [US1] Write **7. ADR Queue** table (ADR | Problem Statement | Affected Specs | Owner | Priority | Source Entry) in `draft/discovery/000-repository-catalog.md` — one row per `Gap? == Yes` and per catalog entry with `BROKEN REFERENCE`/`INCOMPLETE` per FR-007

**Checkpoint**: At this point, User Story 1 is fully functional — the discovery document satisfies SC-001 and SC-003, and the full 7-section structure is ready for review

---

## Phase 4: User Story 2 - Developer evaluates a new dependency against discovery (Priority: P1)

**Goal**: Make the three-question dependency decision procedure usable from the discovery document alone

**Independent Test**: Take 5 sample proposals (MediatR, MassTransit, AutoMapper, new UI framework, new search engine) and verify each reaches a correct allow/deny decision using only the discovery document (SC-002). Prohibited dependencies are caught with BuildingBlocks replacement references.

### Implementation for User Story 2

- [x] T015 [US2] Document the **three-question dependency procedure** (Does draft/* provide it? Does a skill prefer an approach? Does existing NuGet cover it? → only triple-negative permits new dependency) as an introductory paragraph to the Capability Matrix in `draft/discovery/000-repository-catalog.md` per FR-008
- [x] T016 [US2] Add **prohibited-dependency guard rows** to the Capability Matrix in `draft/discovery/000-repository-catalog.md` — at least: "CQRS without MediatR → BuildingBlocks.CQRS ISender + pipeline behaviors", "EventBus without MassTransit → BuildingBlocks.EventBus.RabbitMQ + outbox", "Mapping without AutoMapper → manual mapping per vertical slice", "No cross-module internal access" — with file/namespace refs per FR-009
- [x] T017 [US2] Verify the guard rows make SPEC-001 architecture tests directly authorable: draft 3 example test assertions (commented, not executed) referencing the matrix rows in `specs/001-repository-discovery/quickstart.md` validation notes (or as inline comments in `draft/discovery/000-repository-catalog.md` under Capability Matrix)

**Checkpoint**: At this point, User Stories 1 AND 2 work together — the catalog plus the decision procedure satisfy SC-002 and SC-004

---

## Phase 5: User Story 3 - Feature planner traces later specs to discovery entries (Priority: P2)

**Goal**: Make every later spec (001–014) able to cite discovery entries for foundational decisions

**Independent Test**: Sample SPEC-001 and one other later spec; each must cite at least one discovery catalog entry for a foundational decision (SC-005). Citation anchors (file:line or table row) are present.

### Implementation for User Story 3

- [x] T018 [US3] Add **citation anchors** (file:line or table-row IDs) to every catalog table row in `draft/discovery/000-repository-catalog.md` so that later specs can cite entries explicitly (e.g., `draft/libraries/buildingblocks.md: BuildingBlocks.CQRS → ISender`)
- [x] T019 [US3] Add **Blocked Specs** column values to the ADR Queue in `draft/discovery/000-repository-catalog.md` — each gap row must list the spec(s) it blocks (e.g., ADR for missing AppHost external identity → blocks SPEC-001, SPEC-002) per data-model.md ADR Candidate `blockedSpecs`
- [x] T020 [US3] Cross-reference `draft/refined-specifications.md` Part 0 capabilities with the Capability Matrix in `draft/discovery/000-repository-catalog.md` — ensure every constitution-derived capability from Part 0 appears as a matrix row (or is explicitly marked as "covered by existing code/skills") so downstream specs have a single lookup

**Checkpoint**: All three user stories are independently functional — the full discovery deliverable is citeable and traceable

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and polish that affects all stories

- [x] T021 Run `specs/001-repository-discovery/quickstart.md` validation steps 1–8 against `draft/discovery/000-repository-catalog.md` and fix any rendering or missing-section issues
- [x] T022 Validate Markdown rendering (no broken tables, header separators present, status values exact: FOUND/NOT FOUND/INCOMPLETE/BROKEN REFERENCE/NOT PRESENT) in `draft/discovery/000-repository-catalog.md` per contracts/discovery-schema.md Validation Rules
- [x] T023 Verify every `Gap? == Yes` matrix row has a non-empty ADR link to the ADR Queue in `draft/discovery/000-repository-catalog.md` and update `specs/001-repository-discovery/checklists/requirements.md` if any checklist item was missed

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational completion
  - US1 (P1) and US2 (P1) can then proceed in parallel (different sections of the same output file — coordinate via outline, not file locks; or sequence US1 → US2 if single author)
  - US3 (P2) depends on US1 completing the matrix and ADR queue (needs those rows to add citations/blockedSpecs)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — no dependencies on other stories
- **User Story 2 (P1)**: Can start after Foundational — logically independent from US1, but benefits from US1's matrix being drafted (suggest US1 → US2 order for single author; parallel for team with section merge)
- **User Story 3 (P2)**: Depends on US1 (needs matrix + ADR queue rows to anchor citations)

### Within Each User Story

- Foundational inventories before section writing
- Header before body sections
- Catalog sections before Capability Matrix (matrix consumes catalog findings)
- Matrix before ADR Queue (queue consumes gap flags)
- Story complete before moving to next priority

### Parallel Opportunities

- T003, T004, T005 can run in parallel (different source inventories, same output outline — merge into shared table)
- T007, T008, T009, T010 can run in parallel (different catalog tables, same document — merge under mutex on file)
- T015 and T016 can run in parallel once matrix exists
- Different user stories can be worked by different authors on separate draft branches, merging via the shared document

---

## Parallel Example: User Story 1

```bash
# Once Foundational is done, launch independent catalog sections together:
Task: "Write draft/* Catalog table in draft/discovery/000-repository-catalog.md"        # T008
Task: "Write Skills Catalog table in draft/discovery/000-repository-catalog.md"        # T009
Task: "Write Solution Catalog table in draft/discovery/000-repository-catalog.md"      # T010
# Each writes a different section under the same document — merge sequentially or via git

# Capability Matrix and ADR Queue are sequential (matrix produces ADR queue):
Task: "Write Capability Matrix table in draft/discovery/000-repository-catalog.md"     # T013
Task: "Write ADR Queue table in draft/discovery/000-repository-catalog.md"            # T014
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T002)
2. Complete Phase 2: Foundational (T003-T006) — CRITICAL, blocks all stories
3. Complete Phase 3: User Story 1 (T007-T014)
4. **STOP and VALIDATE**: Run quickstart steps 1-6; verify SC-001 and SC-003
5. Deploy/demo if ready — the discovery document is already usable as the constitutional gate

### Incremental Delivery

1. Setup + Foundational → outline ready
2. Add User Story 1 → review full 7-section catalog → constitutes MVP!
3. Add User Story 2 → dependency procedure usable → SC-002/SC-004 green
4. Add User Story 3 → citeable for downstream specs → SC-005 green
5. Polish → rendering + cross-checks

### Parallel Team Strategy

With multiple authors:

1. Team completes Setup + Foundational together (one author owns the outline)
2. Once Foundational is done:
   - Author A: User Story 1 (catalog sections — the bulk of the document)
   - Author B: User Story 2 (procedure + guard rows — small delta on the matrix)
3. US1 merges, then Author A or B does US3 (citations + blockedSpecs)
4. Polish is a joint review pass

---

## Notes

- [P] tasks = different source inventories or non-overlapping document sections; single output file `draft/discovery/000-repository-catalog.md` requires merge coordination
- [Story] label maps task to specific user story for traceability to FR-002..FR-009 and SC-001..SC-005
- Each user story is independently completable — US1 alone delivers the MVP (a reviewable catalog that unblocks SPEC-001 planning)
- No automated tests are generated for this feature — validation is via quickstart.md steps and checklist review
- Source paths are relative to repository root; the only write target is `draft/discovery/000-repository-catalog.md`
- If `aspire.config.json` is NOT FOUND, that is an expected finding (record status, not an error) per spec Assumptions
