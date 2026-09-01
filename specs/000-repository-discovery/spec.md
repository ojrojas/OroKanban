# Feature Specification: Repository Discovery

**Feature Branch**: `001-repository-discovery`

**Created**: 2026-08-31

**Status**: Draft

**Input**: User description: "SPEC-000 — Repository Discovery. Bounded Context: BC-10 Platform (Generic). Depends on: nothing, Blocks: all other specs. Objective: Convert the constitution's Repository Discovery Gate (Principle I, XIII of Development Lifecycle) into an executable first deliverable. No production feature starts before the discovery document exists. This spec exists so the constitution becomes a concrete architecture instead of guesses about what already exists. Requirements R1-R6 catalog draft/*, skills, solution, AppHost, cross-cutting state and produce discovery document at draft/discovery/000-repository-catalog.md."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Architect performs mandatory discovery before any feature (Priority: P1)

As the platform architect, I want a complete catalog of what already exists in the repository so that every subsequent feature reuses `draft/*` and installed skills instead of reinventing them.

**Why this priority**: This is the constitutional gate (Principle I — Existing Repository Assets Are Authoritative, Principle XXI — TDD+DDD+Vertical Slices, Principle XXII — Workspace Skills Govern Design). Without it, every later spec risks violating the constitution and building on assumptions. It blocks all other specs.

**Independent Test**: Can be fully tested by running the discovery workflow on a clean checkout and verifying the output document exists at `draft/discovery/000-repository-catalog.md` with all six catalog sections populated, with no code generation beyond documentation.

**Acceptance Scenarios**:

1. **Given** a clean repository checkout, **When** the discovery workflow runs, **Then** a document is produced at `draft/discovery/000-repository-catalog.md` containing sections for draft/*, skills, solution, AppHost, cross-cutting state, capability matrix, and ADR queue.
2. **Given** the discovery document exists, **When** a reviewer opens it, **Then** every document under `draft/` is listed with its reusable primitives, endpoints, flows, and configuration knobs summarized.
3. **Given** a gap between constitution requirements and repository reality, **When** discovery identifies it, **Then** it appears as an ADR candidate entry — not as an improvised implementation decision.

---

### User Story 2 - Developer evaluates a new dependency against discovery (Priority: P1)

As a developer proposing a new library or framework, I want a three-question decision procedure so that I only propose a new dependency when no existing asset covers the need.

**Why this priority**: Enforces Principle I's reuse mandate. Prevents fragmentation and keeps the BuildingBlocks canon authoritative.

**Independent Test**: Can be tested by taking 5 sample dependency proposals (e.g., MediatR, MassTransit, AutoMapper, a new UI framework) and verifying the document provides enough information to answer the three questions and reach a correct allow/deny decision.

**Acceptance Scenarios**:

1. **Given** a proposed dependency (e.g., "add MediatR"), **When** the three questions are asked — (1) Does `draft/*` already provide this? (2) Does an installed skill prefer an approach? (3) Does an existing NuGet cover it? — **Then** the answer is determined from the catalog and only a triple-negative permits proposing the dependency.
2. **Given** the catalog lists `BuildingBlocks.CQRS` with `ISender` and pipeline behaviors, **When** MediatR is evaluated, **Then** the catalog shows the capability is already covered and the proposal is denied with a reference to the covering primitive.

---

### User Story 3 - Feature planner traces later specs to discovery entries (Priority: P2)

As a feature planner starting SPEC-001 or any later spec, I want to cite discovery document entries so that architecture decisions are grounded in evidence, not speculation.

**Why this priority**: Creates traceability from constitution → discovery → concrete specs. Required by the acceptance criteria of SPEC-000.

**Independent Test**: Can be tested by opening SPEC-001 draft and verifying each of its foundational decisions (target framework, BuildingBlocks usage, AppHost composition) cites a line/section from the discovery document.

**Acceptance Scenarios**:

1. **Given** the discovery document lists `global.json` target framework and `Directory.Packages.props` versions, **When** SPEC-001 declares .NET 10 and package choices, **Then** those choices cite the discovery catalog entries.
2. **Given** a later spec needs to choose a persistence strategy, **When** it references the discovery catalog's persistence findings, **Then** the choice is recorded as an ADR candidate if the catalog showed a gap.

---

### Edge Cases

- What happens when `draft/` is empty or a referenced document is missing? Discovery must record "NOT FOUND" explicitly for that catalog entry and raise an ADR candidate for the missing canon instead of silently skipping it.
- How does the system handle a skill directory that exists but lacks a SKILL.md? Record the skill name with status "INCOMPLETE — SKILL.md missing" and note its mandate as unknown until resolved.
- What happens when `.agents/skills` contains extra skills beyond the four mandated by Principle XXII (e.g., `aspire-*`, `dotnet-inspect`, `playwright-cli`)? Catalog them all, but clearly flag which four are constitution-mandated vs. supplementary.
- How does discovery handle `OroKanban.slnx` referencing projects that do not exist on disk? Record as "REFERENCE BROKEN" with the expected path and actual filesystem state.
- What happens when `OroKanban.AppHost/AppHost.cs` does not declare an external `oroidentityserver` reference? Record as a gap requiring ADR / integration work — do not auto-generate the integration.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST produce a discovery document at `draft/discovery/000-repository-catalog.md` (creating the `draft/discovery/` directory if it does not exist) that contains all sections defined in FR-002 through FR-007.
- **FR-002**: System MUST enumerate and summarize every document under `draft/` — specifically `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md` — listing for each: purpose, reusable primitives/capabilities, endpoints/flows (where applicable), and configuration knobs. Each finding must include the file path and a concise capability summary.
- **FR-003**: System MUST enumerate every skill directory under `.agents/skills/` and, for each skill (at minimum `dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`), record the mandate it imposes per constitution Principles XXI/XXII. For other installed skills, record their description as supplementary.
- **FR-004**: System MUST catalog the solution structure by inspecting `OroKanban.slnx`, `src/BuildingBlocks/*` project files, `Directory.Build.props`, `Directory.Packages.props`, and `global.json` — recording target frameworks, central package management versions, defined BuildingBlocks projects, and any additional projects referenced by the solution.
- **FR-005**: System MUST catalog orchestration and infrastructure by inspecting `OroKanban.AppHost/AppHost.cs` and `aspire.config.json` (if present) — recording declared Aspire resources, external references (especially `oroidentityserver` integration as external dependency), and connection string / configuration patterns.
- **FR-006**: System MUST catalog cross-cutting state for identity integration, persistence, UI framework, testing, and CI/CD — explicitly recording "NOT PRESENT" / "NOT FOUND" where a capability is absent, so gaps are visible rather than assumed.
- **FR-007**: Discovery document MUST include a **capability matrix** with columns: Needed capability (per constitution and refined specifications Part 0), Provided by `draft/*` (with file reference), Provided by existing code/skills, Gap — and an **ADR queue** listing each gap as a candidate ADR with a short problem statement and the spec(s) it blocks.
- **FR-008**: Discovery process MUST enforce the three-question dependency decision procedure for any proposed new dependency: (1) Does `draft/*` already provide this capability? (2) Does an installed skill establish a preferred approach? (3) Does an existing NuGet dependency already cover it? Only a negative answer to all three permits proposing a new dependency, and the evaluation must be documented in the discovery output.
- **FR-009**: Discovery document MUST be structured so that SPEC-001's architecture tests can be authored directly from it — i.e., it must name prohibited dependencies (MediatR, MassTransit, AutoMapper, cross-module internal access) and the allowed BuildingBlocks replacements with file/namespace references.

### Key Entities

- **Discovery Catalog Entry**: Represents one inspected artifact (a file, skill, or configuration set). Attributes: path, status (FOUND / NOT FOUND / INCOMPLETE / BROKEN REFERENCE), summary of reusable capabilities, configuration surface, and notes.
- **Capability Matrix Row**: Maps a constitution-derived capability need to its coverage. Attributes: needed capability, provided-by-draft reference, provided-by-code/skill reference, gap flag, blocking spec(s).
- **ADR Candidate**: A gap requiring an Architectural Decision Record. Attributes: identifier (e.g., ADR-000), problem statement, affected specs, suggested decision owner, priority.
- **Skill Mandate Record**: One installed skill's governance impact. Attributes: skill name, path, description, constitution principle it enforces, mandated usage scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Discovery document exists at `draft/discovery/000-repository-catalog.md` and every `draft/*` document is cataloged with its reusable capabilities summarized — verifiable by a reviewer in under 5 minutes.
- **SC-002**: Any developer proposing a new dependency can reach a correct allow/deny decision using only the discovery document's three-question procedure — verifiable by walking through 5 sample proposals with zero ambiguity.
- **SC-003**: Every gap between constitution requirements and repository reality appears as an ADR candidate in the queue — zero gaps are silently improvised; verifiable by cross-checking constitution principles against the gap column.
- **SC-004**: SPEC-001's architecture tests can be authored directly from the discovery document without additional inspection — verifiable by drafting at least 3 architecture test cases (prohibited dependency checks, module boundary rules, DbContext inheritance) from discovery content alone.
- **SC-005**: All later specs (001–014) can cite discovery catalog entries for their foundational decisions — verifiable by sampling any later spec's first section for at least one explicit discovery reference.

## Assumptions

- Repository root is the working directory containing `draft/`, `.agents/skills/`, `OroKanban.slnx`, and `OroKanban.AppHost/` as described in the project structure.
- `draft/libraries/buildingblocks.md` and `draft/oroidentityserver-specification.md` are the two canonical rule bases per constitution Principle XXI and are expected to exist; absence is treated as a gap, not a silent omission.
- The four workspace skills mandated by Principle XXII (`dotnet-ai`, `ddd-project-planner`, `minimal-ui-design-system`, `ngrx-signal-store`) are the primary design-time rule bases; additional installed skills are cataloged but do not override the four core mandates.
- Discovery is a documentary deliverable — no runtime code, migrations, or service launches are required to satisfy this spec.
- `aspire.config.json` may not exist in all checkout states; if absent, the AppHost's C# composition is the sole orchestration source of truth.
- The ADR queue produced here is a candidate list; actual ADR authoring happens in subsequent specs per constitution section 15 (Architectural Decision Records).
