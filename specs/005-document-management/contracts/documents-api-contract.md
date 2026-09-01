# Contract: Documents API

**Module**: `Documents` (BC-05) | **Base path**: `/api/documents` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP` 400/403/404/409, tenant-aware, `IEndpoint` per slice, pagination envelope `{items, totalCount, page, pageSize}`.

---

## POST /api/documents — UploadDocument

**Command**: `UploadDocumentCommand : ICommand<Result<UploadDocumentResponse>>`

Multipart form (file upload) or JSON with base64 — preferred: `multipart/form-data` (file) + fields. For OpenAPI: `multipart/form-data` with `file`, `name`, `projectId?`, `workItemId?`, `classificationHint?`, `metadata` JSON fields.

```json
// Request (multipart/form-data fields; file as binary part named "file")
// Fields:
{
  "name": "contract.pdf",                 // string 1–300, required (original filename fallback)
  "projectId": "guid | null",             // optional linkage — validated tenant+exists via IProjectMembership
  "workItemId": "guid | null",            // optional linkage
  "classificationHint": "Confidential | null", // optional hint — final classification resolved by IClassificationPolicy Classification stage
  "mimeType": "application/pdf | null",   // optional — derived from file if missing
  "author": "Jane Doe | null",
  "department": "Finance | null",
  "documentType": "Contract | null",
  "tags": ["finance","q3"] ,              // 0–50, each 1–50 ^[a-z0-9_-]+$ lowercased
  "effectiveDate": "2026-09-01T00:00:00Z | null",
  "expirationDate": "2027-09-01T00:00:00Z | null",
  "retentionDays": 365,
  "customMetadata": { "k1":"v1" }          // ≤50 entries, key ≤64/value ≤2KB
}
// + file part: bytes, size ≤100MB default (per org limit), MIME allow-list enforced
// Response 202 Accepted — Location: /api/documents/{id}
// Body:
{
  "documentId": "guid (DocumentId)",
  "versionId": "guid (DocumentVersionId)",
  "versionNumber": 1,
  "name": "contract.pdf",
  "contentHash": "a3f5...64hex",
  "mimeType": "application/pdf",
  "size": 1234567,
  "classification": "Confidential",
  "ruleVersion": "v3",
  "status": "Uploaded",
  "currentStage": "Validation",
  "tenantId": "guid",
  "projectId": "guid | null",
  "workItemId": "guid | null"
}
// Errors: 400 Validation (name required, MIME disallowed, size exceeded, tags/custom bag invalid, project/workItem tenant mismatch or not found, classificationHint unknown), 401 unauthenticated, 403 forbidden (document.upload), 409 tenant mismatch shadowed as 404
// Side effects (transaction): Document + DocumentVersion(v1, IsPublished await Validation) + DocumentProcessingJob(CurrentStage=Validation, Upload=Succeeded) + outbox DocumentUploadedIntegrationEvent + next-stage event; file NOT stored yet (Storage is stage 6) — binary is staged via IStorageGateway staging or temp hold then PutAsync at Storage stage; SC-001 asserts no VirusScan/Classification in-request
// Idempotency: optional header `Idempotency-Key: guid` — second POST with same key returns same documentId (stored on job)
```

**Domain**: `Document.Create(..., ClassificationHint, MetadataSnapshotDraft, ...)` → `CheckRule(NameValidRule)` + `MimeValidRule` + `SizeRule` + delegate `IClassificationPolicy.AllowedLevels` → `DocumentUploaded` → outbox.

---

## POST /api/documents/{id}/versions — PublishDocumentVersion

**Command**: `PublishDocumentVersionCommand(id, file?, name?, metadataPatch?) : ICommand<Result<PublishVersionResponse>>`

Creates vN+1 with new bytes and/or metadata patch. Always appends version, never mutates prior.

```json
// Request (multipart/form-data; file optional if metadata-only correction)
{
  "file": "binary optional",              // if provided, new ContentHash + Size
  "name": "contract.v2.pdf | null",       // if changing name, updates Document.Name too
  "expectedRowVersion": "base64 | null",  // optimistic concurrency
  "metadata": {                           // same fields as UploadDocument metadata — entire snapshot replacement (patch is full snapshot)
    "author": "...",
    "tags": [...],
    "customMetadata": {...}
  }
}
// Response 201 Created — Location: /api/documents/{id}/versions/{versionNumber}
{
  "documentId": "guid",
  "versionId": "guid (new)",
  "versionNumber": 2,                     // v2
  "contentHash": "9c12...new",
  "previousVersionId": "guid (v1)",
  "name": "contract.v2.pdf",
  "status": "Uploaded",                   // new version starts Uploaded→Validated walk again; job enqueued
  "ruleVersion": "v3"
}
// Errors: 400 validation, 403 (document.upload or document.update), 404 tenant-aware, 409 concurrency (RowVersion mismatch), 422 business rule (Document is Deleted — cannot version)
```

**Domain**: `Document.PublishNewVersion(bytes, metadataSnapshot, actor)` → `CheckRule(VersionIsImmutableOncePublishedRule on prior)` + `CheckRule(CanVersionWhenStatus)` → new `DocumentVersion` + `Document.CurrentVersionId = new.Id` + `DocumentVersionPublished` + prior `DocumentVersionSuperseded` + new `DocumentProcessingJob` for the new version.

---

## POST /api/documents/{id}/classify — ClassifyDocument

**Command**: `ClassifyDocumentCommand(id, classification, reason?) : ICommand<Result<DocumentResponse>>`

Manual (re)classification — typically via Classification pipeline stage automatically, but exposed for approver/operator override. Creates no new version if only classification changes on current `Document` (classification is a `Document` field, not version-snapshot-only) — but if caller also wants metadata change, use `PublishDocumentVersion`.

```json
// Request
{ "classification": "Restricted", "reason": "contains PII", "expectedRowVersion": "base64" }
// Response 200
{ "documentId": "guid", "classification": "Restricted", "ruleVersion": "v3", "status": "Classified" }
// Errors: 400 unknown classification (not in IClassificationPolicy.AllowedLevels), 403 (document.approve/classify permission + IDocumentAccessPolicy classification clearance), 409 concurrency, 422 business rule (Deleted → cannot classify)
```

**Domain**: `Document.Reclassify(newClassification, ruleVersion, actor)` → `CheckRule(ClassificationIsValidRule via IClassificationPolicy)` → `DocumentClassified` via outbox.

---

## POST /api/documents/{id}/approve — ApproveDocument

**Command**: `ApproveDocumentCommand(id, expectedRowVersion?) : ICommand<Result<DocumentResponse>>`

Lifecycle gate: `PendingApproval → Approved` valid; `Approved → Deleted` valid; others rejected.

```json
// Request
{ "expectedRowVersion": "base64 | null" }
// Response 200
{ "documentId": "guid", "status": "Approved", "approvedAt": "2026-09-01T12:00:00Z", "approvedBy": "guid" }
// Errors: 403 (document.approve + subtree/classification clearance via IDocumentAccessPolicy), 422 business rule (illegal transition Approved→Approved etc.), 404, 409
```

**Domain**: `Document.Approve(actor)` → `CheckRule(DocumentStatusTransitionRule(From=PendingApproval, To=Approved))` → `DocumentApproved` via outbox. Approval requires `IDocumentAccessPolicy` approve permission (role + classification + subtree).

---

## DELETE /api/documents/{id} — DeleteDocument

**Command**: `DeleteDocumentCommand(id, expectedRowVersion?) : ICommand<Result<void>>`

Soft delete only — never erases rows. `DocumentStatus → Deleted` (valid from `Available|Classified|PendingApproval|ProcessingFailed`).

```json
// Response 204 No Content
// Errors: 403 (document.delete + clearance), 422 illegal transition (Already Deleted|Archived→Deleted invalid per map), 404, 409
```

**Domain**: `Document.Delete(actor)` → `CheckRule(DocumentStatusTransitionRule)` → `DocumentDeleted` via outbox; `DeletedAt/By` set; subsequent `GetDocument`/`Download` returns 404 to non-auditors (404 shadow) but `GetAccessHistory` as auditor still returns.

---

## GET /api/documents/{id} — GetDocument (authorization-filtered)

**Query**: `GetDocumentQuery(id) : IQuery<Result<DocumentResponse>>`

```json
// Response 200 — authorization-filtered (IDocumentAccessPolicy before return, incluye IsSafe gate)
{
  "id": "guid",
  "tenantId": "guid",
  "organizationId": "guid",
  "ownerId": "guid",
  "name": "contract.pdf",
  "classification": "Confidential",
  "ruleVersion": "v3",
  "currentVersionId": "guid",
  "currentVersionNumber": 2,
  "status": "Available",
  "isSafe": true,                         // false hasta VirusScan clean
  "scanStatus": "Safe",                   // Pending|Safe|Infected|Unavailable
  "scannedAt": "2026-09-01T12:00:00Z",
  "mimeType": "application/pdf",
  "size": 1234567,
  "contentHash": "a3f5...64hex",
  "projectId": "guid | null",
  "workItemId": "guid | null",
  "provenance": { "source":"upload","originalFilename":"contract.pdf","uploadedBy":"guid","uploadedAt":"..." },
  "retention": { "retainUntil":"2027-09-01T00:00:00Z","retentionDays":365,"legalHold":false,"isExpired":false },
  "createdAt": "...",
  "updatedAt": "...",
  "currentStage": "Indexing",             // from DocumentProcessingJob overall/current stage (or Succeeded)
  "stageStatuses": { "Validation":"Succeeded", "VirusScan":"Succeeded", "Metadata":"Succeeded", "Classification":"Succeeded", "Storage":"Succeeded", "Indexing":"InProgress" },
  "downloadUrl": "https://.../presigned?expires=3600 | null" // solo cuando granted + Available/Approved + isSafe=true, short-lived; null si IsSafe=false (NotSafe) o ProcessingFailed → sin binario
}
// Errors: 404 (not found or cross-tenant shadow), 403 filtered as 404 for non-auditors; auditor with document.audit.read may get Deleted docs with status=Deleted; 403 NotSafe cuando IsSafe=false (DocumentAccessDenied reason=NotSafe) aunque Golden Rule A pase
// Side effect on success: DocumentAccessed + DocumentAccessEntry(granted=true); on denial: DocumentAccessDenied + entry(granted=false, reason=NotSafe si IsSafe=false) + no downloadUrl y binary nunca servido desde contenedor
```

**Implementation**: handler loads `Document` + `DocumentVersion (current)` + `DocumentProcessingJob` → calls `IDocumentAccessPolicy.EvaluateAsync(ctx)` → on deny returns `Error.Forbidden` (mapped to 404 shadow for non-auditors) and appends denied entry via `IUnitOfWork`+outbox in same request transaction.

---

## GET /api/documents/{id}/versions — ListDocumentVersions

**Query**: `ListDocumentVersionsQuery(documentId, page=1, pageSize=20) : IQuery<Result<Paged<DocumentVersionResponse>>>`

```json
// Response 200 — filtered by same IDocumentAccessPolicy as GetDocument (caller must be authorized for the document)
{
  "items": [
    { "versionId":"guid","versionNumber":1,"contentHash":"a3...","mimeType":"application/pdf","size":12340,"publishedAt":"...","publishedBy":"guid","ruleVersion":"v3","isSafe":true,"scanStatus":"Safe","scannedAt":"...","metadataSnapshot":{"author":"Jane","tags":["finance"],"documentType":"Contract","effectiveDate":"...","customMetadata":{}} },
    { "versionId":"guid","versionNumber":2,"contentHash":"9c...","mimeType":"application/pdf","size":12500,"publishedAt":"...","publishedBy":"guid","ruleVersion":"v3","isSafe":false,"scanStatus":"Pending","metadataSnapshot":{...} }
  ],
  "totalCount": 2, "page":1, "pageSize":20
}
// Errors: 404, 403 via same shadow, 400 pagination validation — cada versión expone isSafe/scanStatus; isSafe=false indica no lectura de binario aunque metadata visible
```

---

## GET /api/documents/{id}/download/{versionNumber?} — DownloadDocumentVersion (authorization-filtered + binary)

**Query**: `DownloadDocumentQuery(documentId, versionNumber? = current) : IQuery<Result<Stream>>`

- When authorized and `status∈{Available,Approved}` and `isSafe==true` and `DocumentProcessingJob.Storage==Succeeded` → returns `302 Found` to presigned S3 URL or `200` streaming via `IStorageGateway.GetAsync(contentHash)` (gateway verifica `IsSafe` antes de tocar contenedor) with `Content-Type` = `MimeType` + `Content-Disposition: attachment; filename="..."` + `X-Content-Hash` header.
- When `isSafe==false` (`ScanStatus≠Safe`) → `403 NotSafe` (DocumentAccessDenied reason=NotSafe), no binario/URL, aunque Golden Rule A/clasificación/grants pasen — contenedor no sirve blob hasta safe.
- When `status==Processing|ProcessingFailed|Deleted|RetentionExpired` → `404`/`409` with business rule, no binary.
- When unauthorized (Golden Rule A/clasificación) → `404` shadow, no binary, `DocumentAccessDenied` appended.
- All downloads append `DocumentAccessEntry(Action=Download, Granted)`; denials por `NotSafe` también auditan.

---

## Cross-cutting concerns (all endpoints)

- **Tenant isolation**: every handler reads `TenantContext.TenantId` from JWT `tenant_id`; all EF queries via `DocumentByTenantSpec`; cross-tenant id returns `404` (never `403`) — audited as denied with `Reason=TenantMismatch`.
- **Concurrency**: `Document.RowVersion` concurrency token — `PUT`/`POST` mutation endpoints accept `expectedRowVersion` (base64) or `If-Match` header; stale → `409 Conflict` with `Error.Concurrency("Document was modified")`.
- **Validation**: each command has `Validator<T>` (BuildingBlocks `IPipelineBehavior` ValidationBehavior) covering name length, classification allow-list via `IClassificationPolicy.AllowedLevels`, MIME allow-list, tags/custom bag invariants, project/workItem existence (via `IProjectMembership` + `Projects` read model) and tenant match.
- **Auditing**: every success/deny/mutation emits domain event → outbox → `IntegrationEvent` consumed by `Audit` BC (topic `audit.document.*`).
- **Rate limiting**: `UploadDocument` is rate-limited per actor/tenant via `Api` middleware (existing from 002).
- **OpenAPI**: `IEndpoint` slices contribute to `/swagger` via `AddEndpoints`; tenant header documented as `X-Tenant-Id` fallback when JWT lacks claim (dev only).
