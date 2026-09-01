# Contract: RAG Query & Result History

**Module**: `AiProcessing` (BC-06) | **Base path**: `/api/ai` | **Auth**: Bearer JWT (`tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP`, `IEndpoint` per slice, pagination envelope, tenant-aware.

---

## POST /api/ai/rag/query — AskDocumentQuestion (Authorized RAG)

**Command**: `AskDocumentQuestionCommand : ICommand<Result<AskQuestionResponse>>`

Authorized retrieval-augmented generation. Retrieval filters by full authorization stack BEFORE chunks reach the model. Global indexes are forbidden.

```json
// Request
{
  "query": "What is the risk in the contract's delivery clause?",
  "projectId": "guid | null",        // optional scope — if set, retrieval filters to projectId via chunk.metadata.projectId
  "branchScope": "guid | null",      // optional OrganizationUnit id for subtree scope — filters via IManagementHierarchy
  "topK": 5,                         // 1..20, default 5, server caps 20
  "minScore": 0.75,                  // 0..1, default 0.75 (RAG guardrail: relevance threshold)
  "operationType": "QuestionAnswering", // always QuestionAnswering for this endpoint; other operation types use QueueLlmOperation
  "expectedRowVersion": "base64 | null" // not used (stateless query, but accepted for consistency)
}
// Response 200
{
  "query": "What is the risk in the contract's delivery clause?",
  "answer": "The delivery is at risk due to ...",
  "sources": [
    {
      "documentId": "guid",
      "documentVersionId": "guid",
      "chunkId": 3,
      "score": 0.88,
      "classification": "Internal",
      "tenantId": "guid",
      "chunkText": "Delivery must occur before 2026-12-01 ..." // or chunk preview 1..500 chars
    },
    {
      "documentId": "guid",
      "documentVersionId": "guid",
      "chunkId": 1,
      "score": 0.82,
      "classification": "Internal"
    }
  ],
  "provenance": {
    "operationId": "guid (transient RAG operationId, also persisted as LlmOperation with OperationType=QuestionAnswering)",
    "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06" },
    "promptVersion": "v3",
    "createdAt": "2026-09-01T12:00:00Z",
    "createdBy": "guid"
  },
  "qualityIndicator": { "confidence": 0.85, "isInjectionFlagged": false },
  "retrievedChunkCount": 2,
  "filteredOutCount": 3,              // audit: how many unauthorized chunks were pre-filtered
  "correlationId": "guid"
}
// Errors: 400 validation (query 1..2000 chars required, topK out of range, minScore 0..1), 401 unauthenticated, 403 forbidden (ai.rag.query) — but note: scoped retrieval returning 0 chunks is NOT 403; it is 200 with empty sources or 404 with Rag.NoAuthorizedChunks (see below), 404 when no chunks exist tenant-wide, 409 concurrency (rare, but for consistency)
// Zero authorized chunks: returns 404 with Error.NotFound("Rag.NoAuthorizedChunks", "No authorized sources found for this query.") and empty sources — never falls back to global or unauthorized chunks; HTTP body: { "answer": "No authorized sources found", "sources": [], "retrievedChunkCount": 0 }
// Side effect (transaction): LlmOperation(QuestionAnswering) + LlmResult(QuestionAnswering, Content=answer, ChunkReferences=sources, Provenance=...) + outbox RagQueryExecutedIntegrationEvent(retrievedCount, filteredCount, CorrelationId) — even for 0-chunk case an operation is recorded for audit
```

**Pipeline order (enforced)**: (a) `IEmbeddingProvider.GenerateAsync(query)` → embedding; (b) `IAuthorizedRetrievalPolicy.FilteredSearch(embedding, AccessContext(actor, tenant, classification, subtree, project membership, explicit grant), topK, minScore)` which builds metadata filter `tenantId==ctx.TenantId && isSafe==true && status∈{Available,Approved} && isCurrentVersion==true && classificationLevel <= actorMaxLevel && (owner==actor || explicitGrant || IsInSubtree || IsMember)` BEFORE `VectorStore.SearchAsync` ranking — only authorized `ChunkReference`s are ranked; (c) top-K `ChunkReference`s reach `IChatClient` as `User` role chunk boundary (`<document_content>` wrapping), not as instruction; (d) `IChatClient.GetResponseAsync<AnswerSchema>` with `ChatOptions{Temperature=0f, MaxOutputTokens=1024}` + retry 3, model pinned; (e) `IResultValidationPolicy.Validate(documentContent, llmAnswer)` sets `isInjectionFlagged`; (f) persist `LlmResult` + `Provenance` via outbox. Architecture test asserts no `VectorStore.SearchAsync` outside `IAuthorizedRetrievalPolicy`.

---

## GET /api/ai/operations/{operationId}/provenance — already in ai-operations-contract.md (duplicate for discoverability via GET)

Alias: `GET /api/ai/operations/{operationId}` returns same `OperationProvenanceResponse`.

---

## GET /api/ai/results/history — GetResultHistory

**Query**: `GetResultHistoryQuery(documentVersionId, page=1, pageSize=20) : IQuery<Result<Paged<LlmResultHistoryResponse>>>`

Returns chronological `LlmResult`s for a document version, each with provenance + review status. Authorization-filtered: caller must be able to read the source document version (`IDocumentAccessPolicy`).

```json
// Request query string: ?documentVersionId=guid&page=1&pageSize=20
// Response 200
{
  "items": [
    {
      "resultId": "guid (LlmResultId)",
      "operationId": "guid",
      "operationType": "Summarization",
      "tenantId": "guid",
      "documentId": "guid",
      "documentVersionId": "guid",
      "content": "Summary in 3 bullets ...",
      "proposedValue": null, // or { "deadline": "2026-12-31" } for extractions targeting authoritative fields
      "chunkReferences": null, // or [ {chunkId, score} ] for RAG
      "reviewStatus": "PendingReview", // Generated|PendingReview|Approved|Rejected|Superseded
      "provenance": { "sourceDocumentId": "guid", "promptVersion": "v2", "model": { "provider": "azure", "modelName": "gpt-4o-2024-08-06" }, "createdAt": "...", "createdBy": "guid", "processingStatus": "Completed", "qualityIndicator": { "confidence": 0.92 } },
      "createdAt": "2026-09-01T10:00:01Z",
      "supersededBy": null // or guid if Superseded
    },
    {
      "resultId": "guid",
      "operationType": "DeadlineExtraction",
      "reviewStatus": "Approved",
      "provenance": { "promptVersion": "v3" },
      "proposedValue": { "deadline": "2026-12-31" }
    }
  ],
  "totalCount": 2, "page": 1, "pageSize": 20
}
// Ordering: ASC by CreatedAt (audit chronological); paginated
// Errors: 404 tenant-aware (document version not found or cross-tenant), 403 via IDocumentAccessPolicy (caller cannot read source document — filtered to 0 items or 403), 400 pagination validation
// Historical fidelity: each result's PromptVersion is immutable snapshot — GetResultHistory after prompt advances still shows old results with old version
```

---

## GET /api/ai/reviews/pending — ListPendingReviews

**Query**: `ListPendingReviewsQuery(reviewerId? = self, tenantId, page=1, pageSize=20, operationType? = all) : IQuery<Result<Paged<PendingReviewResponse>>>`

Returns `LlmResult`s whose `ReviewStatus==PendingReview` and where `IReviewPolicy.RequiresReview==true` at generation time, filtered by reviewer scope (reviewer can read source document).

```json
// Request: ?reviewerId=guid&page=1&pageSize=20&operationType=DeadlineExtraction
// Response 200
{
  "items": [
    {
      "resultId": "guid",
      "operationId": "guid",
      "operationType": "DeadlineExtraction",
      "documentId": "guid",
      "documentVersionId": "guid",
      "documentName": "contract.pdf",
      "classification": "Confidential",
      "tenantId": "guid",
      "content": "Extracted deadline: 2026-12-31 ...",
      "proposedValue": { "deadline": "2026-12-31" },
      "reviewStatus": "PendingReview",
      "provenance": { "model": { "provider": "azure" }, "promptVersion": "v2" },
      "createdAt": "2026-09-01T10:00:01Z"
    }
  ],
  "totalCount": 1, "page": 1, "pageSize": 20
}
// Authorization: reviewer must have ai.review.approve permission + Golden Rule A read on each source document; results where reviewer cannot read source are not returned (filtered before fetch). Pagination: page beyond total → empty items with correct totalCount.
// Errors: 403 (ai.review.approve required), 404 tenant shadow, 400 pagination
```

---

## Cross-cutting (RAG)

- **Chunk metadata filtering**: every chunk stored has `tenantId`, `classificationValue`, `projectId`, `isSafe`, `isCurrentVersion` indexed for pre-filter; `isSafe==false` or `status Deleted` chunks are never returned.
- **Injection check**: `IResultValidationPolicy` flags payload containing `Ignore previous instructions` etc.; `QualityIndicator.isInjectionFlagged` is in `provenance.qualityIndicator`; answer is sanitized (injected directive stripped).
- **Architecture gate**: test `VectorStoreQueryMustGoThroughAuthorizedPolicy` — scanning codebase for `VectorStore.SearchAsync` outside `IAuthorizedRetrievalPolicy` fails the build.
