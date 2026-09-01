---
name: ddd-project-planner
description: Turns a business idea into a complete, ready-to-build project plan using Domain-Driven Design — domain discovery, bounded contexts, context map, ubiquitous language, aggregates/entities/value objects, architecture recommendation, backlog, user stories with Given/When/Then acceptance criteria, TDD strategy, and a sprint roadmap. Use this skill whenever the user wants to plan a new software product, describes a business idea and asks for a technical plan, asks for DDD modeling (bounded contexts, aggregates, ubiquitous language, context map), asks to turn an idea into a backlog/roadmap/sprints, or mentions planning a SaaS, ERP, CRM, marketplace, or enterprise system from scratch — even if they don't use the words "DDD" or "planner" explicitly. Also use it if the user asks to add "enterprise-level" rigor to a plan (ADRs, Event Storming, C4 model, NFR matrix, traceability matrix).
---

# DDD Project Planner

Turns a business idea into a complete project plan: domain model, architecture, backlog, user
stories, TDD strategy, and sprint roadmap — delivered as a single Markdown document.

## Step 1 — Gather input

Before planning, make sure you have (ask only for what's missing — don't re-ask what the user
already told you, and don't block on nice-to-haves):

- **Business description**: what does the product do, who is it for
- **Product type**: SaaS, Enterprise, Marketplace, ERP, CRM, internal tool, etc.
- **Users**: main user roles/personas
- **Technical constraints**: anything that's fixed (compliance, existing systems to integrate, deadlines)
- **Tech stack**: preferred or required languages/frameworks (if none given, propose one in Step 4 and say so explicitly)
- **Expected scale**: rough user/traffic volume, or "not sure yet" is fine

If the user gives you a rich description up front, extract these from it rather than asking again.
Only ask a clarifying question when a genuinely different plan would result depending on the
answer (e.g., B2B vs B2C changes the whole domain model). Otherwise state your assumption inline
and proceed.

**Enterprise mode**: turn on the "Enterprise" additions (see Step 7) only if the user explicitly
signals this is a corporate/enterprise-scale project — e.g. they used the word "enterprise",
mention multiple integrated systems, compliance/audit needs, a large org, or ask for ADRs/Event
Storming/C4/NFRs by name. Otherwise, skip Step 7 entirely and keep the plan lean.

## Step 2 — Business analysis

Work through this like a Business Analyst would, and capture it briefly at the top of the output:

- **Objectives**: what business outcome this product exists to achieve
- **Stakeholders**: who cares about this system and why
- **Key processes**: the main business processes the system supports
- **Business rules**: constraints/policies that shape the domain (these often become domain invariants later)
- **Risks**: technical, business, or adoption risks worth flagging early

## Step 3 — Domain discovery and DDD strategic design

Think through this like a Domain Expert followed by a DDD Architect:

1. **Domain classification** — identify the Core Domain (the competitive-advantage part),
   Supporting Domains, and Generic Domains (solved problems, e.g. auth, email — candidates for
   off-the-shelf solutions).
2. **Ubiquitous Language** — a short glossary of domain terms as the business would say them, used
   consistently for the rest of the document.
3. **Bounded Contexts** — split the domain into contexts (e.g. Identity, Billing, Inventory,
   Sales, Notifications, Reporting). Name each one, state its responsibility, and note which
   domain classification it belongs to (core/supporting/generic).
4. **Context Map** — show how contexts relate and depend on each other (e.g. Customer → Sales →
   Billing → Accounting), including the relationship type where relevant (Shared Kernel,
   Customer-Supplier, Anti-Corruption Layer, etc.) if the user's domain calls for that level of detail.

## Step 4 — Domain modeling and architecture

For each bounded context that matters to an MVP (don't force-model trivial/generic contexts in
detail), produce:

- **Entities** — objects with identity and lifecycle
- **Value Objects** — immutable objects defined by their attributes
- **Aggregates** — consistency boundaries, with their aggregate root
- **Domain Events** — meaningful things that happen (e.g. `OrderPlaced`, `InvoiceIssued`)
- **Domain Services / Policies** — logic that doesn't naturally belong to one entity

Then propose the application and infrastructure shape:

- **Application layer**: commands, queries, handlers — keep this to a representative sample per
  context, not exhaustive
- **Infrastructure needs**: persistence, messaging, cache, external APIs, storage, email — only
  what the domain actually requires
- **Architecture style recommendation**: pick one (Clean Architecture, Hexagonal/Ports & Adapters,
  Modular Monolith, Microservices, Vertical Slice) based on project size and team size, and briefly
  justify the choice — don't default to microservices for a small project.
- **Tech stack**: use what the user specified; if unspecified, propose a stack and say it's a
  suggestion the user can swap out.

## Step 5 — Backlog and user stories

Structure: **Epic → Features → Stories → Tasks**.

For each user story use this exact template:

```
### [Story ID] Story title

**As** <role>
**I want** <capability>
**So that** <business value>

**Acceptance Criteria**
- Given <context>, When <action>, Then <outcome>
- (add more Given/When/Then lines as needed)
```

Group stories by epic, and make sure every epic maps back to a bounded context from Step 3 so the
backlog and the domain model stay traceable to each other.

## Step 6 — TDD strategy and sprint roadmap

**TDD plan**: for each major use case, list the tests needed across levels — Unit, Integration,
Contract, E2E — and note the Red → Green → Refactor cycle applies throughout. Don't write actual
test code; this is a test *plan*, listing what needs coverage and why.

**Sprint roadmap**: group backlog epics into sprints in a sensible dependency order (e.g.
Authentication & Users first, since most other contexts depend on identity). For each sprint list:
sprint goal, contexts/features covered, and any dependencies on earlier sprints. Use judgment on
sprint count based on project size — don't force a fixed number.

## Step 7 — Enterprise additions (only if Enterprise mode is on)

Add these sections when triggered (see Step 1):

- **Event Storming summary** — key domain events in business-process order (Big Picture level),
  plus a Design-Level pass for the core domain's most complex flow
- **ADRs (Architecture Decision Records)** — one short ADR per major architectural decision made in
  Step 4 (context, decision, consequences)
- **Quality Attributes / NFR matrix** — a table of non-functional requirements (performance,
  availability, security, maintainability, observability, scalability) with a target and how it's
  addressed
- **Traceability matrix** — a table linking business objectives → epics → stories → use cases, so
  every story can be traced back to a business reason
- **C4 model summary** — Context and Container level descriptions (Component/Code level only if the
  user asks, since it needs actual code to be meaningful)
- **DevOps plan** — CI/CD approach, containerization, infra-as-code, observability (logging,
  tracing), feature flags
- **Security plan** — auth approach (OAuth2/OIDC/JWT), authorization model (RBAC/ABAC), audit
  logging, encryption at rest/in transit, secrets management, and an OWASP Top 10 checklist relevant
  to this system

## Output

Deliver the plan as a **single Markdown file** (use the docx skill instead only if the user
explicitly asks for a Word document). Structure it as one document with clear `##` headers in this
order:

```
1. Vision & Business Analysis
2. Ubiquitous Language
3. Domain Discovery (Core/Supporting/Generic)
4. Bounded Contexts & Context Map
5. Domain Model (per context: entities, value objects, aggregates, events)
6. Architecture (style + justification, application layer, infrastructure)
7. Backlog (epics → features → stories, with acceptance criteria)
8. TDD Strategy
9. Sprint Roadmap
10. [Enterprise mode only] Event Storming, ADRs, NFR Matrix, Traceability Matrix, C4 Summary, DevOps Plan, Security Plan
11. Risks & Technical Debt (call out anything deferred or simplified for the MVP)
```

Keep every section proportional to the project's actual size — a small SaaS idea should not
produce the same depth as a multi-context enterprise system. When in doubt, favor being complete
over being short: this document is meant to be handed to a dev team to start building from.
