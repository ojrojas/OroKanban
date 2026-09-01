# Contract: Audit Search, Trail, and Timeline

**Module**: `Audit` (BC-08) | **Base path**: `/api/audit` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP` 400/403/404/409, tenant-aware `Specification<AuditEntry>` pre-filter via `IAuditQueryAuthorization` (Golden Rule A subtree+project+grant) BEFORE fetch, pagination envelope `{items, totalCount, page, pageSize, link}`, masked snapshots `***`.

---

## GET /api/audit/entries — SearchAuditEntries

**Query**: `SearchAuditEntriesQuery : IQuery<Result<Paged<AuditEntryResponse>>>`

Filters audit entries with authorization pre-filter (least-privilege, cross-branch filtered out).

```json
// Request query string (all filters optional except tenant from JWT):
// ?actorId=guid&action=DocumentApproved&resourceType=Document&resourceId=guid&projectId=guid&organizationId=guid&from=2026-08-25T00:00:00Z&to=2026-09-01T23:59:59Z&result=Success&correlationId=guid&page=1&pageSize=50
// Response 200 — ordered Timestamp desc, paginated, authorization-filtered
{
  "items": [
    {
      "auditId": "guid (AuditEntryId)",
      "timestamp": "2026-09-01T12:00:01Z",
      "actor": { "actorId": "guid", "actorType": "User", "displayName": "Alice Manager" },
      "action": "DocumentApproved",
      "resourceType": "Document",
      "resourceId": "guid",
      "organizationId": "guid | null",
      "tenantId": "guid",
      "result": "Success",
      "errorCode": null,
      "correlationId": "guid",
      "clientMetadata": { "ipAddress": "192.168.1.xxx", "userAgent": "Mozilla/5.0..." },
      "beforeAfterSnapshot": { "before": { "status": "PendingApproval" }, "after": { "status": "Approved", "apiKey": "***" } },
      "previousHash": "64 hex | null",
      "hash": "64 hex | null"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50,
  "link": "<http://localhost:5000/api/audit/entries?page=2&pageSize=50&...>; rel=\"next\""
}
// Ordering: DESC by Timestamp (audit search); paginated — page beyond total returns empty items with correct totalCount
// Filtering: each filter is optional WHERE predicate; when multiple present they AND (e.g., actorId==Alice AND action==DocumentApproved)
// Authorization: results are WHERE audit.TenantId==ctx.TenantId AND (audit.OrganizationId IS NULL OR audit.OrganizationId IN subtree(Auditor_A)) AND (audit.ProjectId IS NULL OR audit.ProjectId IN authorizedProjectIds) — so an auditor in branch A sees zero entries for OrgUnit_B/P_B. Cross-branch `OrganizationId=Org_B` when caller is `Org_A` subtree → filtered out (not 403 enumeration — filtered before fetch, but if OrganizationId explicitly for forbidden branch and caller has no entries for it, totalCount=0 not 403). However unauthenticated/unauthorized caller (no auditor|manager role and not owner) → 403 with audited AuditSearchDenied.
// Tenant isolation: WHERE TenantId==ctx.TenantId; tenant mismatch → 404 shadow (never 403) to avoid enumeration.
// Performance: composite INDEX (TenantId, Timestamp DESC) + (ResourceType, ResourceId) + (CorrelationId) ensures <300ms p95 for 1k paginated (EXPLAIN ANALYZE verified).
// Errors: 400 Validation (page/pageSize 1..100, from>to → Audit.DateRangeInvalid), 401 unauthenticated, 403 Forbidden (auditor scope, also audited as AuditSearchDenied with correlationId), 404 tenant shadow (when tenant not found)
```

**Handler**: `SearchAuditEntriesQueryHandler` does: (1) `IAuditQueryAuthorization.CanActorQuery(actorId, tenantId, filters)` → if false `Error.Forbidden` (and same-tx outbox `AuditSearchDenied` integration event → `AuditEntry` with `Action=AuditSearchDenied` — audited attempt); (2) `var authFilter = IAuditQueryAuthorization.BuildAuthorizedFilter(actorId, tenantId)` → `Expression<Func<AuditEntry,bool>>` tenant+subtree+project; (3) `var spec = new AuditByTenantSpec(tenantId).And(authFilterSpec).And(new ActorFilterSpec(actorId?)).And(new ActionFilterSpec(action?)).And(new ResourceFilterSpec(resourceType?,resourceId?))...` with `ApplyOrderByDescending(a=>a.Timestamp)` + `ApplyPaging((page-1)*pageSize, pageSize)` + `AsNoTracking`; (4) `IRepository<AuditEntry>.FindAsync(spec)` via `SpecificationEvaluator` + `IRepository.CountAsync` for `totalCount`; (5) map `AuditEntry` → `AuditEntryResponse` DTO (masked snapshot already masked at persistence, never raw).

---

## GET /api/audit/trail/{resourceType}/{resourceId} — GetAuditTrail

**Query**: `GetAuditTrailQuery(resourceType, resourceId) : IQuery<Result<Paged<AuditEntryResponse>>>`

Returns chronological trail for a single resource (`ResourceType+ResourceId`) ordered `Timestamp` asc (audit trail), authorization-filtered (caller must be able to read the resource per Golden Rule A or be auditor for its tenant/branch).

```json
// Request: GET /api/audit/trail/Document/guid?tenantId=guid&page=1&pageSize=50 (tenant from JWT, resourceId from path)
// Response 200 — ordered Timestamp asc (trail chronological)
{
  "items": [
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:00Z", "action": "DocumentUploaded", "actor": { "actorId": "guid" }, "result": "Success", "correlationId": "guid", "beforeAfterSnapshot": { "before": null, "after": { "documentId": "guid" } } },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:05Z", "action": "DocumentApproved", "actor": { "actorId": "guid" }, "result": "Success", "correlationId": "guid" }
  ],
  "totalCount": 2, "page": 1, "pageSize": 50
}
// Authorization: handler calls IAuditQueryAuthorization.BuildAuthorizedFilter + Where(ResourceType==rt && ResourceId==rid) + TenantId; if caller lacks authorizedOrgIds for the resource's OrganizationId (e.g., resource in OrgUnit_B but caller is OrgUnit_A subtree) → 404 shadow (not 403 enumeration) with totalCount=0 but HTTP 404 for GET (not 403) to avoid leak that resource exists but is forbidden. However if caller is owner of the resource (e.g., Document OwnerId == actorId) they see trail even without global auditor role (OR over owner, subtree manager, auditor role — per IAuditQueryAuthorization).
// Performance: INDEX (ResourceType, ResourceId, Timestamp ASC) + TenantId predicate.
// Errors: 400 ResourceType 1..100 / ResourceId 1..200, 401, 404 shadow (cross-branch/cross-tenant resource or resource not found — indistinguishable)
```

---

## GET /api/audit/timeline/{correlationId} — GetOperationTimeline

**Query**: `GetOperationTimelineQuery(correlationId) : IQuery<Result<IReadOnlyList<AuditEntryResponse>>>`

Returns all audit entries sharing `CorrelationId` ordered `Timestamp` asc across resource types, enabling full distributed workflow reconstruction (`HTTP → storage → processing → indexing → LLM → review`).

```json
// Request: GET /api/audit/timeline/guid?tenantId=guid (tenant from JWT, correlationId from path)
// Response 200 — ordered Timestamp asc across resource types
{
  "correlationId": "guid",
  "items": [
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:00Z", "action": "DocumentUploaded", "resourceType": "Document", "resourceId": "guid", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:02Z", "action": "DocumentProcessingStageCompleted", "resourceType": "DocumentProcessingJob", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:05Z", "action": "DocumentApproved", "resourceType": "Document", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:06Z", "action": "LlmOperationQueued", "resourceType": "LlmOperation", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:10Z", "action": "LlmResultGenerated", "resourceType": "LlmResult", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:12Z", "action": "LlmReviewCreated", "resourceType": "LlmReview", "result": "Success", "correlationId": "guid" },
    { "auditId": "guid", "timestamp": "2026-09-01T10:00:13Z", "action": "DocumentAccessDenied", "resourceType": "Document", "result": "Denied", "correlationId": "guid" }
  ],
  "totalCount": 7
}
// Authorization: same IAuditQueryAuthorization BuildAuthorizedFilter + Where(CorrelationId==cid AND TenantId==ctx.TenantId AND (OrganizationId IN authorizedOrgIds OR ProjectId IN authorizedProjectIds)) — so timeline only returns entries the caller is authorized to see (branch-filtered). If correlationId spans both branch A and B entries, caller in branch A sees only branch A entries (zero from B); caller with tenant-wide auditor sees all 7. No pagination for timeline (correlationId typically has <100 entries per workflow); if >100, server truncates at 100 with Link.
// Errors: 400 correlationId required valid Guid, 401, 404 shadow (no entries for correlationId OR all entries filtered out by authorization — indistinguishable from non-existent correlationId to avoid enumeration)
```

**Cross-cutting**: All three queries share `AuditEntryResponse` DTO (never domain `AuditEntry`):

```json
{
  "auditId": "guid",
  "timestamp": "DateTime UTC ISO-8601",
  "actor": { "actorId": "guid", "actorType": "User|System|Anonymous", "displayName": "string 1..200" },
  "action": "DocumentApproved | string 1..100 (AuditAction)",
  "resourceType": "Document",
  "resourceId": "string",
  "organizationId": "guid | null",
  "tenantId": "guid",
  "result": "Success|Denied|Failed",
  "errorCode": "string | null",
  "correlationId": "guid",
  "clientMetadata": { "ipAddress": "string | null", "userAgent": "string | null" },
  "beforeAfterSnapshot": { "before": {}, "after": {} }, // always masked, JsonDocument *** replacements
  "previousHash": "64 hex | null",
  "hash": "64 hex | null"
}
```

**Tenant isolation**: `WHERE TenantId==ctx.TenantId` is first predicate in `BuildAuthorizedFilter`; tenant mismatch → 404 (never 403).

**Pagination**: `page` 1..100, `pageSize` 1..100 default 50 cap 100, `Link` header `rel="next"` when `skip+take < totalCount`.

**Hash chaining exposure**: `PreviousHash`/`Hash` are 64 hex or null — client can call `GET /api/audit/verify-chain?tenantId=guid` (future) to get `VerifyChain()` result; this spec exposes them read-only for `VerifyChain()` client reconstruction.

