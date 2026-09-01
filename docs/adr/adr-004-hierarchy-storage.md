# ADR-004: Hierarchy Storage — Recursive CTE on Adjacency List

**Date**: 2026-08-31
**Status**: Accepted
**Deciders**: Platform architect
**Context**: `draft/discovery/000-repository-catalog.md` gap ADR-002 — OrganizationDbContext was bare, hierarchy storage strategy undecided (recursive CTE vs closure table vs ltree). Spec 003 requires `IManagementHierarchy` (IsInSubtree/GetSubtree/GetAncestors/GetCommonAncestor) with arbitrary depth, no cycles, tenant isolation, and 500 ms p95 on 1,000-user hierarchy.

**Decision**: Store `ManagementRelationship` as adjacency list `organization.management_relationships(manager_id, subordinate_id, type, valid_from, valid_to, organization_unit_id, tenant_id)` with indexes `(tenant_id, manager_id)` and `(tenant_id, subordinate_id)` plus filtered unique index for single active per subordinate/unit. Implement `IManagementHierarchy` with **PostgreSQL recursive CTEs** (`WITH RECURSIVE`) over active rows (`valid_from <= now AND (valid_to IS NULL OR now <= valid_to)`). Schema `organization` via `HasDefaultSchema("organization")`.

**Consequences**:
- Writes O(1) (single row per relationship), reads O(depth) — acceptable for depth <10 and 1,000-user scale, meets 500 ms p95 with warm Redis (research Decision 2).
- No second table (closure) to keep consistent, no Postgres extension (ltree) dependency.
- Can be migrated to closure table later without changing domain contract (`IManagementHierarchy` is the only query path).

**Alternatives considered**: Closure table (O(1) reads, O(N) writes on re-parent, second table consistency), ltree (extension dependency), in-memory only (violates persistence).

**References**: specs/003-identity-access-organization/research.md Decision 1, data-model.md ManagementRelationship, contracts/hierarchy-contract.md
