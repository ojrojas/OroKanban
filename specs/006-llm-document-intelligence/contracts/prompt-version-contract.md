# Contract: Prompt Version Lifecycle

**Module**: `AiProcessing` (BC-06) | **Base path**: `/api/ai/prompts` | **Auth**: Bearer JWT | **Conventions**: `Result→HTTP`, `IEndpoint` per slice.

---

## POST /api/ai/prompts — already in ai-operations-contract.md (canonical)

---

## GET /api/ai/prompts — ListPromptVersions

**Query**: `ListPromptVersionsQuery(operationType, page=1, pageSize=20) : IQuery<Result<Paged<PromptVersionResponse>>>`

```json
// Request: ?operationType=Summarization&page=1&pageSize=20
// Response 200
{
  "items": [
    { "promptVersionId": "guid", "operationType": "Summarization", "versionNumber": 1, "template": "Summarize {{content}} ...", "isPublished": true, "publishedAt": "2026-09-01T10:00:00Z", "publishedBy": "guid" },
    { "promptVersionId": "guid", "operationType": "Summarization", "versionNumber": 2, "template": "Summarize with risks {{content}} ...", "isPublished": true, "publishedAt": "2026-09-01T11:00:00Z", "publishedBy": "guid" }
  ],
  "totalCount": 2, "page": 1, "pageSize": 20
}
// Ordering: ASC by VersionNumber (audit chronological)
// Errors: 400 unknown operationType, 403 (ai.prompt.publish read requires auth but list is authenticated-only), 404 tenant shadow (tenant has no prompts)
```

---

## GET /api/ai/prompts/{promptVersionId} — GetPromptVersion

**Query**: `GetPromptVersionQuery(promptVersionId) : IQuery<Result<PromptVersionResponse>>`

```json
// Response 200
{
  "promptVersionId": "guid",
  "operationType": "Summarization",
  "versionNumber": 2,
  "template": "Summarize with risks {{content}} ...",
  "isPublished": true,
  "publishedAt": "2026-09-01T11:00:00Z",
  "publishedBy": "guid",
  "rowVersion": "base64"
}
// Errors: 404 (not found or cross-tenant shadow), 403 (authenticated required)
// Immutability invariant: GET after PUT/PATCH attempt on published version shows template unchanged; PUT/PATCH endpoints do not exist — only POST (publish new version) is allowed (append-only).
```

---

## Immutability Contract

- `LlmPromptVersion` once `IsPublished=true` is immutable: domain setter `Template` throws `PromptIsImmutableOncePublishedRule` via `CheckRule` if `IsPublished`.
- Changing a prompt requires `POST /api/ai/prompts` which does `VersionNumber = max(OperationType)+1` (monotonic). No `PUT/PATCH /api/ai/prompts/{id}` exists — architecture test asserts no update path for published versions.
- Historical `LlmResult.Provenance.PromptVersion` is a string/id snapshot (e.g., `v1`) not a live FK; `GetResultHistory` after `v3` still shows `v1` for old results — verified by integration test `PromptImmutabilityPreservesHistory`.
- Concurrency: `PublishPromptVersion` uses `RowVersion` on read of `maxVersion` — second writer with stale max gets 409 and retries with fresh max.
