# Contract: Document Access History

**Module**: `Documents` (BC-05) | **Base path**: `/api/documents/{id}/history` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Scope**: auditor (`document.audit.read`) or owner (`Document.OwnerId == actorId`) — others → 403/404 shadow

---

## GET /api/documents/{id}/history — GetAccessHistory

**Query**: `GetAccessHistoryQuery(documentId, page=1, pageSize=50, action?: Read|Download|Denied) : IQuery<Result<Paged<DocumentAccessEntryResponse>>>`

Returns the append-only access history for a document — reads, downloads, and denials with actors and timestamps (acceptance criterion: auditor query).

```json
// Request query string:
// ?page=1&pageSize=50&action=Denied  (filter optional)
// Response 200 — chronologically ordered (oldest first for audit trail; latest paginated fallback)
{
  "items": [
    {
      "id": "guid (entry)",
      "documentId": "guid",
      "tenantId": "guid",
      "actorId": "guid",
      "action": "Denied",                 // Read|Download|Denied
      "granted": false,
      "classification": "Confidential",
      "ruleVersion": "v3",
      "reason": "InsufficientClassification", // NotInSubtreeOrMembership|InsufficientClassification|Deleted|TenantMismatch|NotFound|ExplicitlyRevoked
      "timestamp": "2026-09-01T12:00:03Z",
      "ipAddress": "10.0.0.1 | null",
      "userAgent": "Mozilla/... | null"
    },
    {
      "id": "guid",
      "actorId": "guid",
      "action": "Read",
      "granted": true,
      "classification": "Confidential",
      "ruleVersion": "v3",
      "reason": null,
      "timestamp": "2026-09-01T12:01:00Z"
    },
    {
      "id": "guid",
      "action": "Download",
      "granted": true,
      "classification": "Confidential",
      "ruleVersion": "v3",
      "timestamp": "2026-09-01T12:02:00Z"
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 50
}
// Ordering: ASC by Timestamp (audit chronological); paginated — page beyond total returns empty items with correct totalCount
// Filtering: ?action=Denied returns only denials; ?action=Read returns granted reads only
// Classification included: value at time of access (level + ruleVersion) — not current document classification
```

**Authorization**: `GetAccessHistory` is scoped: succeeds only when `actor == Document.OwnerId` OR `IAuthorizationEvaluator.CanActorPerform(actor, "document.audit.read")` (auditor role) — otherwise `403 Error.Forbidden` (and the attempt itself is audited as a denied history access via `DocumentAccessDenied` with reason `HistoryAccessDenied`). Cross-tenant `documentId` returns `404` shadow.

**Tenant isolation**: `WHERE DocumentId==id AND TenantId==ctx.TenantId`; tenant mismatch → 404.

**Audit source**: history rows are `documents.document_access_entries` (append-only), populated by `DocumentAccessed`/`DocumentAccessDenied` handlers via same-tx outbox — never live-computed from logs.

---

## DocumentAccessEntryResponse DTO

```json
{
  "id": "guid",
  "documentId": "guid",
  "tenantId": "guid",
  "actorId": "guid",
  "action": "Read|Download|Denied",
  "granted": "bool",
  "classification": "string 1–100",
  "ruleVersion": "string 1–20",
  "reason": "string? max 200 — generic, no internal detail leak",
  "timestamp": "DateTime UTC ISO-8601",
  "ipAddress": "string? 1–45",
  "userAgent": "string? max 500"
}
```

- `action=Denied` always has `granted=false` and a non-null `reason`; `granted=true` entries have `reason=null`.
- `classification` + `ruleVersion` capture the value at time of access — they do not drift when policy advances after the event.

---

## History also includes history-query denials

Per spec edge: calling `GetAccessHistory` without auditor/owner role is itself a denial that appends a `DocumentAccessDenied` entry to the same table (with `Action=Denied` and `Reason=HistoryAccessDenied`) and returns `403`. Subsequent auditor queries will see that denial as evidence.
