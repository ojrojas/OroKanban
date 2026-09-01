# Tasks: Audit, Monitoring and Compliance

**Input**: Design documents from `/specs/007-audit-monitoring-compliance/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/
**Branch**: `007-audit-monitoring-compliance`

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Modular monolith**: `src/Modules/Audit/` (4-layer: Domain, Application, Infrastructure, Contracts), `src/Api/Program.cs`, `src/Web/src/app/features/audit/`, `tests/` at repo root
- Paths shown below assume modular layout per `plan.md` — adjust if `specs/007-audit-monitoring-compliance/plan.md` structure changes

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and Audit module plumbing

- [X] T001 Create Audit module directory structure per `plan.md` in `src/Modules/Audit/` (Domain, Application, Infrastructure, Contracts subfolders with initial csproj references and `Directory.Packages.props` central pinning)
- [X] T002 Add audit/monitoring dependencies via central package management in `Directory.Packages.props` and `src/Modules/Audit/Audit.Infrastructure/Audit.Infrastructure.csproj` (`Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `Serilog.Sinks.OpenTelemetry` — already via ServiceDefaults, just verify)
- [X] T003 Configure Audit options binding with `IOptions` via `src/Modules/Audit/Audit.Infrastructure/Configuration/AuditOptions.cs` (`Audit:MaskedFields` default `ApiKey,Password,Secret,ConnectionString,Token,CreditCard,PrivateKey` extensible, `Audit:HashChainingEnabled` bool per ADR-007-01, `Audit:RetentionDays` optional)
- [X] T004 Wire AuditDbContext and CorrelationIdMiddleware registration in `src/Api/Program.cs` (AddDbContext<AuditDbContext> HasDefaultSchema `audit`, `CorrelationIdMiddleware` X-Correlation-Id → TenantContext.CorrelationId → Activity.Baggage before UseAuthentication)
- [X] T005 [P] Scaffold `Audit.Tests` project via `dotnet new xunit` style in `tests/Audit.Tests/Audit.Tests.csproj` (xUnit, NSubstitute, Testcontainers.PostgreSql, NetArchTest, Microsoft.AspNetCore.TestHost, OpenTelemetry Test Helpers)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Create StronglyTypedId for AuditEntry in `src/Modules/Audit/Audit.Domain/Ids/AuditEntryId.cs` (`AuditEntryId : StronglyTypedId<Guid>`, `AuditConsumedEventId` not needed — `EventId` is Guid directly)
- [X] T007 [P] Create AuditAction enumeration (31 values R2 catalog) in `src/Modules/Audit/Audit.Domain/Enumerations/AuditAction.cs` (AuthenticationSucceeded=1..AuditCorrected=31, `Enumeration<AuditAction>` with `FromId/FromName`, maps 1:1 to domain events per ADR-007-03 table)
- [X] T008 [P] Create AuditActorType and AuditResultType enumerations in `src/Modules/Audit/Audit.Domain/Enumerations/AuditTypes.cs` (AuditActorType `User=1|System=2|Anonymous=3`, AuditResultType `Success=1|Denied=2|Failed=3`)
- [X] T009 [P] Create ActorReference and ResourceReference value objects in `src/Modules/Audit/Audit.Domain/ValueObjects/ActorResourceReference.cs` (`ValueObject` ActorReference `ActorId` Guid, `ActorType`, `DisplayName` 1..200; ResourceReference `ResourceType` 1..100, `ResourceId` 1..200, `GetEqualityComponents` stable order)
- [X] T010 [P] Create BeforeAfterSnapshot and AuditResult value objects in `src/Modules/Audit/Audit.Domain/ValueObjects/AuditSnapshot.cs` (`ValueObject` BeforeAfterSnapshot `BeforeJson`/`AfterJson` `JsonDocument` 1..50k masked, `GetEqualityComponents` normalized; AuditResult `Result` + `ErrorCode` 1..100 optional)
- [X] T011 [P] Create domain services interfaces pure in `src/Modules/Audit/Audit.Domain/Services/IAuditServices.cs` (`IAuditMaskingPolicy` `Mask(BeforeAfterSnapshot)→masked`, `IAuditQueryAuthorization` `CanActorQuery` + `BuildAuthorizedFilter(actorId,tenantId)→Expression<Func<AuditEntry,bool>>` — no I/O except injected IManagementHierarchy/IProjectMembership reads)
- [X] T012 Create domain events or marker for audit production (no AuditEntry domain event — terminal record) in `src/Modules/Audit/Audit.Domain/Events/AuditDomainEvents.cs` (document that AuditEntry is terminal; no `RaiseDomainEvent` from AuditEntry — it is produced by consumer)
- [X] T013 [P] Create business rules placeholder via `IBusinessRule` if needed in `src/Modules/Audit/Audit.Domain/Rules/AuditBusinessRules.cs` (AuditEntryIsImmutableRule conceptual — enforced by zero setters, not CheckRule; plus `DateRangeInvalidRule` for search inverted range)
- [X] T014 Configure AuditDbContext with HasDefaultSchema `audit` and Outbox + IsImmutable simulation in `src/Modules/Audit/Audit.Infrastructure/Persistence/AuditDbContext.cs` (`AppDbContextBase`, `OnModelCreating` HasDefaultSchema("audit") + ApplyConfiguration(new OutboxEntityTypeConfiguration()) + ApplyConfiguration(new AuditEntryConfiguration()) + ApplyConfiguration(new AuditConsumedEventConfiguration()), no Update/Delete mapping for AuditEntry)
- [X] T015 [P] Create EF entity type configurations in `src/Modules/Audit/Audit.Infrastructure/Persistence/Configurations/AuditEntityConfigurations.cs` (AuditEntry: `audit.audit_entries` PK `AuditId`, `Timestamp` indexed desc `(TenantId,Timestamp DESC)`, `ActorId`, `Action` int, `ResourceType/ResourceId` composite `(ResourceType,ResourceId,Timestamp ASC)`, `TenantId` indexed, `CorrelationId` indexed, `BeforeJson/AfterJson` jsonb masked, `PreviousHash`/`Hash` 64 hex nullable, `RowVersion` IsConcurrencyToken but immutable)
- [X] T016 Create AuditConsumedEvent dedup entity and configuration in `src/Modules/Audit/Audit.Infrastructure/Persistence/Configurations/AuditConsumedEventConfiguration.cs` (`audit.audit_consumed_events` PK `EventId` Guid UNIQUE, `ProcessedAt` UTC, `Action` string, `CorrelationId` Guid)
- [X] T017 Create integration event contracts placeholder (audit consumer subscribes to all `*IntegrationEvent`) in `src/Modules/Audit/Audit.Contracts/Events/AuditIntegrationEvents.cs` (no new events from Audit — it consumes `DocumentApprovedIntegrationEvent` etc. with `CorrelationId` Guid added per R3; document mapping table comment)
- [X] T018 [P] Create core specifications with tenant filtering in `src/Modules/Audit/Audit.Infrastructure/Specifications/AuditSpecifications.cs` (AuditByTenantSpec, AuditByTenantAndResourceSpec `Where(a=>a.TenantId==tenantId && a.ResourceType==rt && a.ResourceId==rid)`, AuditByCorrelationIdSpec `Where(a=>a.CorrelationId==cid)`, AuditByTenantAndTimestampRangeSpec — all `Specification<AuditEntry>` with `Where` tenantId predicate, cross-tenant 404 helper)
- [X] T019 Implement CorrelationIdMiddleware and health check skeleton in `src/Api/Middleware/CorrelationIdMiddleware.cs` and `src/Modules/Audit/Audit.Infrastructure/Health/AuditHealthChecks.cs` (Middleware: `X-Correlation-Id` header → `TenantContext.CorrelationId` + `Activity.Current.SetBaggage("CorrelationId")` + `Response.Headers["X-Correlation-Id"]`, generates Guid if absent; Health: `AddHealthChecks().AddCheck<NpgsqlHealthCheck>("postgres")` etc. per contract health-metrics-contract.md, but actual per-dependency checks wired in Polish)

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Append-only audit trail for every catalogued action (Priority: P1) 🎯 MVP

**Goal**: Every R2 catalog action (22 distinct types) → one immutable AuditEntry via domain→outbox→integration→audit consumer idempotent on EventId, with actor/resource/result/correlationId/masked snapshot, queryable within 2s, duplicate EventId → zero second entry.

**Independent Test**: Perform one action per R2 category with common CorrelationId C1 (failed login, denied GetDocument, created project, moved work item, uploaded document, denied access, approved AI result) → SearchAuditEntries as auditor scoped to those resources filter CorrelationId=C1 → one entry per action with non-null AuditId, Timestamp≈now, Actor==performer, ResourceType/ResourceId==target, Result Success|Denied|Failed, CorrelationId==C1, BeforeAfterSnapshot masked (ApiKey==***). Re-deliver same EventId → zero additional entries (ConsumerIdempotencyTests). CatalogCompletenessTests 22 actions.

### Tests for User Story 1 (TDD — write FAIL before impl)

- [X] T020 [P] [US1] Unit test for CatalogCompleteness pending (does contract cover 22 actions) in `tests/Audit.Tests/Unit/CatalogCompletenessTests.cs` (iterate AuditAction.GetAll() 31 values, assert each maps to at least one IntegrationEvent type per audit-events-contract.md table)
- [X] T021 [P] [US1] Integration test for consumer idempotency duplicate delivery → one entry in `tests/Audit.Tests/Integration/ConsumerIdempotencyTests.cs` (Testcontainers Postgres + RabbitMQ or NSubstitute IEventBus, publish same IntegrationEvent Id twice, assert audit_entries count for that EventId ==1, second HandleAsync returns success without side effect, UniqueConstraintViolation treated as success)
- [X] T022 [P] [US1] Integration test for emission path and correlation propagation end-to-end in `tests/Audit.Tests/Integration/CorrelationPropagationTests.cs` (TestServer with CorrelationIdMiddleware, X-Correlation-Id: C1 → TenantContext.CorrelationId == C1 → Activity.Baggage C1 → DomainEvent CorrelationId C1 → IntegrationEvent CorrelationId C1 → AuditEntry CorrelationId C1, assert chain equality)

### Implementation for User Story 1

- [X] T023 [P] [US1] Create AuditEntry aggregate (append-only, immutable) in `src/Modules/Audit/Audit.Domain/Aggregates/AuditEntry.cs` (AggregateRoot<AuditEntryId> with AuditId PK Guid, Timestamp UTC, Actor ActorReference VO, Action AuditAction Enumeration, ResourceType 1..100, ResourceId 1..200, OrganizationId Guid?, TenantId Guid, Result AuditResult VO (Success|Denied|Failed + ErrorCode), CorrelationId Guid, ProjectId Guid?, ClientMetadata IpAddress/UserAgent, BeforeAfterSnapshot VO masked, PreviousHash/Hash 64 hex nullable, no public setters — constructor only, no Update/Delete mutators, RaiseDomainEvent not needed as terminal)
- [X] T024 [US1] Implement AuditEventConsumer background service idempotent in `src/Modules/Audit/Audit.Infrastructure/Consumers/AuditEventConsumer.cs` (IHostedService / IIntegrationEventHandler<IntegrationEvent> generic, handles audit.* wildcard, logic: SELECT 1 FROM audit_consumed_events WHERE EventId=@event.Id FOR UPDATE → if exists return; INSERT INTO audit_consumed_events (EventId, ProcessedAt) in AuditDbContext transaction; IAuditMaskingPolicy.Mask(beforeAfterSnapshot) → masked; INSERT INTO audit_entries with AuditId=NewGuid(), Timestamp=UtcNow, Actor from event, Action via AuditAction.FromIntegrationEventType, ResourceType/ResourceId from event, CorrelationId from event.CorrelationId ?? Activity.Baggage, same transaction, SaveChangesAsync atomic, manual ack after commit, catch UniqueConstraintViolation on EventId → rollback but ACK as success, handles 100 events/sec)
- [X] T025 [US1] Implement Api/Audit contracts DTOs in `src/Modules/Audit/Audit.Contracts/DTOs/AuditDtos.cs` (SearchAuditEntriesRequest with ActorId?, Action?, ResourceType?, ResourceId?, ProjectId?, OrganizationId?, DateRange From/To UTC, Result?, CorrelationId?, page/pageSize 1..100, PagedResultEnvelope<T>, AuditEntryResponse with BeforeAfterSnapshot masked JsonDocument, AuditId, Timestamp, Actor, Action, ResourceType/ResourceId, OrganizationId, TenantId, Result, ErrorCode, CorrelationId, PreviousHash/Hash, Link header)
- [X] T026 [US1] Configure outbox publishing for all producer DbContexts to include CorrelationId in `src/Modules/Audit/Audit.Infrastructure/Outbox/CorrelationIdOutboxEnricher.cs` (extend IOutboxWriter.StageAsync to set IntegrationEvent.CorrelationId = TenantContext.CorrelationId ?? Guid.Parse(Activity.Current?.Baggage.FirstOrDefault(b=>b.Key=="CorrelationId").Value ?? Guid.NewGuid().ToString()), every concrete *IntegrationEvent in Documents/Organization/AiProcessing has Guid CorrelationId param added per domain-events-contract.md)

**Checkpoint**: At this point, US1 append-only trail is fully functional: any R2 action within 2s has one AuditEntry with correct actor/resource/correlationId/masked snapshot, duplicate EventId → zero second entry, catalog completeness verifiable.

---

## Phase 4: User Story 2 - Immutability and tamper-evidence (Priority: P1)

**Goal**: AuditEntry immutable by design — no Update/Delete path exists (domain no setters, IRepository only AddAsync, DbContext no Modified tracking, DB REVOKE UPDATE/DELETE for app role or hash-chain verification), corrections are new entries with Action=AuditCorrected, tamper detectable via VerifyChain() or REVOKE error.

**Independent Test**: Load persisted AuditEntry E1 via repository and attempt every mutation vector: domain E1.Action = "Tampered" → compile fail (no setter), repository.Update(E1) → not found (no method), dbContext.AuditEntries.Update(E1) → compile fail or NotSupportedException at SaveChanges, reload GetAuditTrail(resourceId=D) still original Action. Direct SQL UPDATE audit_entries SET action='Tampered' → fails REVOKE or is detectable via VerifyChain() PreviousHash mismatch if hash chaining adopted per ADR-007-01.

**Acceptance Scenarios**: Verify corrections new entries with SequenceNumber/HashChain incremented, VerifyChain recomputes SHA256 and returns Error.DataCorrupted on tamper, integration test AuditEntryIsImmutable via reflection.

### Tests for User Story 2

- [X] T027 [P] [US2] Unit test for immutability via reflection (no public setters) in `tests/Audit.Tests/Unit/AuditEntryIsImmutableTests.cs` (typeof(AuditEntry).GetProperties().Count(p=>p.SetMethod?.IsPublic==true)==0, IRepository<AuditEntry> interface has only AddAsync + Find methods, no Update/Remove, assert dbContext model has no EntityState.Modified path for AuditEntry)
- [X] T028 [P] [US2] Integration test for corrections as new entries in `tests/Audit.Tests/Integration/CorrectionTests.cs` (persist E1 with Result=Success, then correction AuditCorrected with ResourceId=E1.AuditId, BeforeAfterSnapshot {Before: Success, After: CorrectedResult}, verify E1 untouched, timeline shows both ordered Timestamp, SequenceNumber incremented)
- [X] T029 [P] [US2] Integration test for tamper detection (hash chain or REVOKE) in `tests/Audit.Tests/Integration/TamperDetectionTests.cs` (if hash chaining enabled per ADR-007-01: insert E1 with Hash=SHA256(PreviousHash||AuditId||Timestamp||Action||Actor), attempt direct SQL tamper, VerifyChain() detects mismatch → Error.DataCorrupted; if REVOKE: direct UPDATE → PostgresException permission denied)

### Implementation for User Story 2

- [X] T030 [P] [US2] Enforce immutability at domain model level in `src/Modules/Audit/Audit.Domain/Aggregates/AuditEntry.cs` (ensure no public setters, make setter private or init-only, remove any Update method, keep constructor as sole mutator, add comment // Immutable — no Update/Delete mutators by design, archive via AuditCorrected)
- [X] T031 [US2] Restrict repository interface to AddAsync only in `src/Modules/Audit/Audit.Infrastructure/Repositories/AuditEntryRepository.cs` (EfRepository<AuditEntry> wrapper exposing only AddAsync(AuditEntry) + FindAsync/Spec, no Update/Remove;DbContext AuditEntries DbSet configured with UsePropertyAccessMode FieldDuringConstruction + no Entry(entity).State=Modified path for AuditEntry via OnModelCreating not mapping update)
- [X] T032 [US2] Apply DB hardening per ADR-007-01 in `src/Modules/Audit/Audit.Infrastructure/Persistence/Migrations/20260901_AddRevokeOrHashChain.cs` (either REVOKE UPDATE, DELETE ON audit.audit_entries FOR app_orokanban + GRANT SELECT, INSERT ON audit.* TO app_orokanban, or add PreviousHash/Hash columns computed in same transaction SELECT Hash FROM audit_entries WHERE TenantId=@t ORDER BY Timestamp DESC LIMIT 1 FOR UPDATE tail lock, Hash=SHA256(PreviousHash|AuditId|Timestamp|Action|Actor) — nullable 64 hex forward-compatible)
- [X] T033 [US2] Implement correction flow as new entry in `src/Modules/Audit/Audit.Application/Features/Correction/CreateAuditCorrectionCommand.cs` (ICommand<Result<AuditEntryResponse>> CreateAuditCorrectionCommand(CorrectedAuditId, CorrectedResult, Rationale, TenantId, ActorId) : Validator rationale 1..2000, Handler creates new AuditEntry Action=AuditCorrected, ResourceId=correctedAuditId, BeforeAfterSnapshot masked, same tx outbox — no update of E1, returns 201)
- [X] T034 [US2] Implement VerifyChain query for tamper detection in `src/Modules/Audit/Audit.Application/Features/VerifyChain/VerifyChainQuery.cs` (IQuery<Result<VerifyChainResponse>> VerifyChainQuery(TenantId) : Handler SELECT AuditId, PreviousHash, Hash, Timestamp, Action, ActorId FROM audit_entries WHERE TenantId=@t ORDER BY Timestamp ASC, recomputes SHA256 chain, returns first mismatch {AuditId, ExpectedHash, ActualHash} or Success — exposed as GET /api/audit/verify-chain?tenantId=guid)

**Checkpoint**: Immutability is structurally impossible: zero public setters, no repository Update path, DB REVOKE or hash mismatch detection, corrections are new AuditCorrected entries, VerifyChain detects tamper.

---

## Phase 5: User Story 3 - Authorization-filtered audit search and per-resource trails (Priority: P1)

**Goal**: Search audit entries by 8 filters (actor/action/resource/project/org/date/result/correlationId) + per-resource trail + per-correlation timeline, all filtered by Golden Rule A before fetch (tenant+subtree+project+grant, deny-by-default, OR over grants), so cross-branch entries filtered out (branch isolation) and unauthenticated → 403 + audited AuditSearchDenied.

**Independent Test**: Seed audit entries for two branches under same tenant with disjoint subtrees: Branch A (manager Alice owns project P_A) E_A1(ProjectCreated), E_A2(DocumentAccessDenied on P_A), Branch B E_B1 on P_B; as Alice (scoped to subtree A + project P_A), SearchAuditEntries actor=Alice dateRange last 7d → only E_A1,E_A2 zero E_B1, GetAuditTrail(resourceId=P_B)→404/empty (not 403), GetOperationTimeline(correlationId=C_B)→404; as super-auditor tenant-wide → all three visible. Filter combinations individually asserted (actor+action+resource+project+org+date+result+correlationId). Unauthorized caller → 403 and audit AuditSearchDenied.

### Tests for User Story 3

- [X] T035 [P] [US3] Unit test for query authorization composition (Golden Rule A) in `tests/Audit.Tests/Unit/QueryAuthorizationTests.cs` (IAuditQueryAuthorization.CanActorQuery with fakes IManagementHierarchy.IsInSubtree false for branch B when actor in A, BuildAuthorizedFilter returns Expression<Func<AuditEntry,bool>> tenant+OrganizationId IN subtree(A)+ProjectId IN authorizedProjects, deny-by-default, OR over grants, value-equality)
- [X] T036 [P] [US3] Integration test for search filters 8 dims with tenant+subtree pre-filter in `tests/Audit.Tests/Integration/SearchFiltersTests.cs` (Testcontainers Postgres, seed audit_entries for Tenant T, call SearchAuditEntries with each filter individually and combined actor+action+resource+project+org+date+result+correlationId, assert WHERE TenantId==T AND OrganizationId IN subtree AND paginated ordered Timestamp desc, cross-branch filtered out)
- [X] T037 [P] [US3] Security integration test for cross-branch audit search MUST NOT surface other branch in `tests/Audit.Tests/Security/CrossBranchAuditSearchTests.cs` (seed 2 branches disjoint subtrees, 5 actor types × 2 branches, assert resultForForbiddenBranch==0 for SearchAuditEntries and GetAuditTrail 404 shadow, same filter as IDocumentAccessPolicy but scoped to audit OrganizationId)
- [X] T038 [P] [US3] Integration test for correlation propagation end-to-end in `tests/Audit.Tests/Integration/CorrelationPropagationTests.cs` (TestServer with CorrelationIdMiddleware X-Correlation-Id: C1 → TenantContext.CorrelationId==C1 → Activity.Baggage C1 → DomainEvent CorrelationId C1 → IntegrationEvent CorrelationId C1 → AuditEntry CorrelationId C1, assert chain equality)

### Implementation for User Story 3

- [X] T039 [P] [US3] Implement IAuditQueryAuthorization pure domain service in `src/Modules/Audit/Audit.Domain/Services/AuditQueryAuthorization.cs` (interface + pure implementation AuditQueryAuthorization : IAuditQueryAuthorization with injected Funcs IsInSubtree, IsMember, HasExplicitGrant, method CanActorQuery(actorId, tenantId, filters) → bool and BuildAuthorizedFilter(actorId, tenantId) → Expression<Func<AuditEntry,bool>> tenant==tenantId AND (OrganizationId==null OR OrganizationId IN GetSubtreeIds(actorId)) AND (ProjectId==null OR ProjectId IN GetProjectIds(actorId) ∪ explicitGrants), no I/O except fakes, deny-by-default)
- [X] T040 [US3] Implement AuditQueryAuthorization infrastructure adapter with hierarchy/project in `src/Modules/Audit/Audit.Infrastructure/Services/AuditQueryAuthorizationService.cs` (adapter injecting IManagementHierarchy + IProjectMembership + IAuthorizationEvaluator (CanActorPerform audit.search) + reading explicit grants via documents.document_explicit_grants read-only, builds filter sets authorizedOrgIds = IsInSubtree-derived subtree(actorId) or allOrgsInTenant if auditor role, authorizedProjectIds = GetProjectIds(actorId), composes Expression)
- [X] T041 [US3] Implement SearchAuditEntries query with 8 filters and pagination in `src/Modules/Audit/Audit.Application/Features/Search/SearchAuditEntriesQuery.cs` (IQuery<Result<Paged<AuditEntryResponse>>> SearchAuditEntriesQuery(ActorId?, Action?, ResourceType?, ResourceId?, ProjectId?, OrganizationId?, From?, To?, Result?, CorrelationId?, Page=1, PageSize=50, TenantId) : Validator page/pageSize 1..100, from>to → Audit.DateRangeInvalid, Handler: CanActorQuery → if false Error.Forbidden + same-tx outbox AuditSearchDenied integration→AuditEntry with Action=AuditSearchDenied; then var authFilter=BuildAuthorizedFilter, var spec=new AuditByTenantSpec(tenantId).And(new AuthorizedFilterSpec(authFilter)).And(new ActorFilterSpec(actorId?)).And(...Resource...).And(new DateRangeSpec(from,to)).And(...Correlation...), ApplyOrderByDescending(a=>a.Timestamp) + ApplyPaging((page-1)*pageSize, pageSize) + AsNoTracking, FindAsync+CountAsync, map to AuditEntryResponse DTOs masked already, Link header for next page derived from totalCount vs skip+take, Result→HTTP 400/403/404/409, 404 shadow cross-branch)
- [X] T042 [US3] Implement GetAuditTrail and GetOperationTimeline queries in `src/Modules/Audit/Audit.Application/Features/Trail/GetAuditTrailQuery.cs` and `src/Modules/Audit/Audit.Application/Features/Timeline/GetOperationTimelineQuery.cs` (GetAuditTrailQuery(ResourceType, ResourceId, TenantId, Page) : Specification AuditByTenantAndResourceSpec ResourceType+ResourceId + authFilter before fetch, ordered Timestamp asc, 404 shadow if all filtered out; GetOperationTimelineQuery(CorrelationId, TenantId) : Where(CorrelationId==cid AND TenantId==t AND OrganizationId IN authorizedOrgIds) ordered Timestamp asc across types, returns 7-entry workflow, truncated at 100 with Link, unauthorized filtered timeline returns 404 shadow indistinguishable from non-existent correlationId)
- [X] T043 [US3] Expose audit search IEndpoints with authorization pre-filter before fetch in `src/Modules/Audit/Audit.Application/Features/Search/AuditSearchEndpoints.cs` (GET /api/audit/entries?actorId&action&resourceType&resourceId&projectId&organizationId&from&to&result&correlationId&page&pageSize, GET /api/audit/trail/{resourceType}/{resourceId}, GET /api/audit/timeline/{correlationId} : each MapGet with Result→HTTP mapping 400 validation, 403 generic denial with audited AuditSearchDenied, 404 tenant-aware shadow for trail/timeline, never 403 enumeration when resource hidden)

**Checkpoint**: Audit search is authorization-gated before fetch: 8 filters ANDed with BuildAuthorizedFilter tenant+subtree+project, cross-branch filtered out (P_B absent for Alice, visible for super-auditor), unauthorized → 403 + audited, cross-tenant 404 shadow.

---

## Phase 6: User Story 4 - Operational monitoring and health dashboards via Aspire + OTel (Priority: P2)

**Goal**: Service health per dependency identifiable (postgres/rabbitmq/redis/ai_provider/vector_store each distinct IHealthCheck), failed requests, background jobs (document_processing, ai_processing), queue depth, latency, DB errors, authorization failures via Aspire dashboard + OTel backends, observable via /health (readiness) vs /alive (liveness).

**Independent Test**: With AddServiceDefaults() OTel flow, GET /health returns Healthy when postgres/rabbitmq/redis/ai_provider reachable; simulate outage (stop postgres or stub IChatClient to throw ProviderUnavailable) → /health→Unhealthy with Entries["postgres"] Unhealthy and Entries["rabbitmq"] Healthy distinctly, /alive remains Healthy (liveness vs readiness split). Dashboard shows QueueDepth gauge for document_processing and ai_processing topics, Latency histogram for QueueLlmOperation <300ms p95, AuthorizationFailure counter increments on 403, logs correlate via CorrelationId baggage traceId.

### Tests for User Story 4

- [X] T044 [P] [US4] Unit test for per-dependency health check mapping in `tests/Audit.Tests/Unit/HealthPerDependencyTests.cs` (NpgsqlHealthCheck SELECT 1 with masked ConnectionString, RabbitMqHealthCheck CreateConnection ping, RedisHealthCheck PingAsync, AiProviderHealthCheck GetResponseAsync("ping") with Temperature=0, VectorStoreHealthCheck count query — each implements IHealthCheck, HealthCheckResult Healthy|Unhealthy with Exception.Message ***-masked for secrets, Architecture test asserts AddHealthChecks count ==5 distinct registrations)
- [X] T045 [P] [US4] Integration test for health endpoints liveness vs readiness split in `tests/Audit.Tests/Integration/HealthEndpointsTests.cs` (TestServer with AddServiceDefaults, GET /health 200 Healthy when all Entries Healthy, GET /alive 200 Healthy always; fault inject postgres SocketException → GET /health 503 Unhealthy with Entries["postgres"]=Unhealthy and Entries["rabbitmq"]=Healthy distinct, not aggregated 503 alone)
- [X] T046 [P] [US4] Integration test for metrics emission (failed requests, job failed, queue depth, latency) in `tests/Audit.Tests/Integration/MetricsTests.cs` (Meter OroKanban.Metrics: http_requests_total Counter tags endpoint/tenantId, http_requests_failed_total Counter tags status 403/500 endpoint, job_failed_total Counter tags job=document_processing stage=VirusScan tenantId, rabbitmq_queue_depth ObservableGauge tags queue, http_request_duration_ms Histogram tags endpoint — scrape Prometheus text format and assert counts, latency histogram bucket le="300" for QueueLlmOperation)

### Implementation for User Story 4

- [X] T047 [P] [US4] Implement per-dependency IHealthCheck registrations in `src/Api/Program.cs` and `src/Modules/Audit/Audit.Infrastructure/Health/AuditHealthChecks.cs` (AddHealthChecks() .AddCheck<NpgsqlHealthCheck>("postgres", HealthStatus.Unhealthy, tags: new[] { "ready", "db" }) .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: new[] { "ready", "messaging" }) .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready", "cache" }) .AddCheck<AiProviderHealthCheck>("ai_provider", tags: new[] { "ready", "ai" }) .AddCheck<VectorStoreHealthCheck>("vector_store", tags: new[] { "ready", "vector" }), each IHealthCheck with timeout 5s, Unhealthy on exception, Exception.Message ***-masked for ConnectionString/ApiKey)
- [X] T048 [US4] Wire health endpoints liveness vs readiness split in `src/Api/Program.cs` (app.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse, Predicate = check => check.Tags.Contains("ready") }) → 503 if any ready Unhealthy distinct per Entries; app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = _ => false }) or AddCheck("self") only → 200 Healthy unless process dead; HealthCheckOptions ResponseWriter writes Application/json with status, totalDuration, entries {postgres: {status, description, exception, data}} )
- [X] T049 [US4] Implement metrics and OTel instrumentation via AddServiceDefaults in `src/Api/Program.cs` (AddServiceDefaults() adds AddOpenTelemetry().WithMetrics(m => m.AddMeter("OroKanban.Metrics").AddAspNetCoreInstrumentation().AddRuntimeInstrumentation().AddNpgsqlInstrumentation()).WithTracing(t => t.AddSource("OroKanban.Api").AddAspNetCoreInstrumentation()) → PrometheusExporter on /metrics or OTLP to Aspire dashboard Metrics/Traces/Logs; define Meter OroKanban.Metrics in src/BuildingBlocks/BuildingBlocks.ServiceDefaults/Metrics/MetricsRegistry.cs with Counter<long> http_requests_total, http_requests_failed_total, job_failed_total, ObservableGauge rabbitmq_queue_depth, Histogram<double> http_request_duration_ms — increment in middleware/health checks/audit consumer)
- [X] T050 [US4] Implement structured logging with Serilog OTLP correlation in `src/BuildingBlocks/BuildingBlocks.Logger/SerilogConfigurator.cs` or `src/Api/Program.cs` (Serilog AddSerilog + Enrich.WithCorrelationId (Baggage CorrelationId) → logs [INF] HTTP POST /api/documents 202 CorrelationId=guid TenantId=guid Actor=guid (traceId=..., spanId=...) and AuditEventConsumer processed EventId=guid AuditId=guid CorrelationId=guid Action=DocumentUploaded Result=Success, Aspire dashboard Logs + Traces correlate via traceId and CorrelationId baggage)

**Checkpoint**: Health is per-dependency identifiable: postgres down → Entries["postgres"]=Unhealthy while rabbitmq=Healthy, /alive still Healthy, metrics http_requests_failed_total/job_failed_total/rabbitmq_queue_depth observable in Aspire dashboard Metrics.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Hardening, observability refinement, and validation across all audit/monitoring stories

- [X] T051 [P] Add OTel tracing baggage and audit correlation enrichment in `src/Modules/Audit/Audit.Infrastructure/Observability/AuditTracingEnricher.cs` (ActivitySource OroKanban.Audit, enrich spans with auditEntryId/correlationId/action tags, Baggage CorrelationId propagation verified, Activity.Baggage + TenantContext.CorrelationId fallback)
- [X] T052 [P] Implement global exception handling for audit queries with Result→HTTP mapping in `src/BuildingBlocks/BuildingBlocks.ServiceDefaults/Exceptions/GlobalExceptionHandler.cs` (IExceptionHandler maps Error.Validation→400, Error.Forbidden→403 generic without reason leak, Error.NotFound→404 shadow for cross-branch/trail, Error.DataCorrupted (hash mismatch)→500 with masked message)
- [X] T053 [P] Add pagination Link header helper and date range validation in `src/Modules/Audit/Audit.Application/Common/AuditPaginationHelper.cs` (BuildLinkHeader(page, pageSize, totalCount, Request.QueryString) → <http://.../api/audit/entries?page=2&pageSize=50&...>; rel="next" when skip+take < totalCount; DateRange From>To → Audit.DateRangeInvalid 400)
- [X] T054 [P] Write quickstart validation script covering SC-001..005 in `specs/007-audit-monitoring-compliance/quickstart-validation.sh` (bash script executing curl flows: catalog completeness 22 actions with CID, immutability no setters, filtered search cross-branch, timeline 7 entries, health per-dependency 503 vs alive 200 — uses /tmp/audit.json + psql $DATABASE_URL audit.audit_entries count, X-Correlation-Id header propagation)
- [X] T055 [P] Add EF composite indexes verification via EXPLAIN ANALYZE in `src/Modules/Audit/Audit.Infrastructure/Persistence/AuditDbContext.cs` (ensure AuditEntryConfiguration creates INDEX (TenantId, Timestamp DESC), INDEX (ResourceType, ResourceId, Timestamp ASC), INDEX (CorrelationId, Timestamp ASC), INDEX (TenantId, OrganizationId), INDEX (TenantId, ProjectId) — verify <300ms p95 for 1k paginated via EXPLAIN ANALYZE in test)
- [X] T056 [P] Perform end-to-end build and test validation in `tests/Audit.Tests/` (run `dotnet build OroKanban.slnx -warnaserror` and `dotnet test tests/Audit.Tests -v minimal && dotnet test tests/Architecture -v minimal` — all 4 stories green, architecture gates pass: AuditEntry zero public setters, IRepository<AuditEntry> only AddAsync, all Audit queries via IAuditQueryAuthorization, tenant predicate required, no IRepository<AuditEntry>.Update path compiled)
- [X] T057 [P] Document ADRs for hash chaining and alerting backend in `docs/adr/ADR-007-01-audit-hash-chaining.md` and `docs/adr/ADR-007-02-alerting-backend.md` (ADR-007-01: SHA256(PreviousHash||AuditId||Timestamp||Action||Actor) tail lock vs REVOKE UPDATE,DELETE + VerifyChain() Error.DataCorrupted contract; ADR-007-02: Prometheus AlertManager vs Grafana vs OTel alerts topic for health Unhealthy >1m → Slack/Pager, plus ADR-007-03 retention/purge)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately (Audit module scaffold, OTel/CorrelationIdMiddleware, AuditOptions)
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories (StronglyTypedId, AuditAction 31, VOs, IAuditMaskingPolicy/IAuditQueryAuthorization interfaces, immutable AuditEntry, REVOKE/hash columns, AuditConsumedEvent dedup, AuditByTenantSpec, CorrelationIdMiddleware)
- **User Stories (Phase 3+)**: All depend on Foundational phase completion
  - **US1 (P1) Append-only trail** can start first - no story dependencies, creates AuditEntry + AuditEventConsumer core
  - **US2 (P1) Immutability** depends on US1 AuditEntry aggregate (immutability via zero setters) but logically parallelizable after T023 - shares AuditEntry domain but enforcement is via reflection/arch test independent
  - **US3 (P1) Filtered search** depends on US1 AuditEntry + Foundational IAuditQueryAuthorization interface - adds BuildAuthorizedFilter pre-filter
  - **US4 (P2) Monitoring** depends on foundational AddServiceDefaults but not on audit search - can parallelize with US3 after Foundational (health checks use separate AuditHealthChecks, not AuditEntry)
- **Polish (Phase 7)**: Depends on all 4 stories complete for E2E correlation timeline + per-dependency health distinct

### User Story Dependencies

- **US1 (P1) Append-only trail**: Can start after Foundational - No other story dependencies - Produces AuditEntry + AuditEventConsumer + 22-action catalog + CorrelationId propagation
- **US2 (P1) Immutability**: Requires US1 AuditEntry aggregate (T023) - adds immutability enforcement (no setters, REVOKE/VerifyChain, AuditCorrected)
- **US3 (P1) Filtered search**: Requires US1 AuditEntry + US2 immutability but logically parallelizable after IAuditQueryAuthorization interface - adds SearchAuditEntries/GetAuditTrail/GetOperationTimeline with 8 filters + Golden Rule A pre-filter
- **US4 (P2) Monitoring**: Requires Foundational AddServiceDefaults but not US1 audit - adds per-dependency health, metrics, logs via Aspire dashboard (reuses Verification of US1 audit trail as dual observability)

### Within Each User Story

- Tests (if TDD) MUST be written and FAIL before implementation (the `[P]` tests in each US phase: CatalogCompletenessTests → ConsumerIdempotencyTests, AuditEntryIsImmutableTests → CorrectionTests, QueryAuthorizationTests → CrossBranchAuditSearchTests, HealthPerDependencyTests → HealthEndpointsTests)
- Aggregates/VOs before domain services
- Domain services before application handlers
- Handlers before IEndpoints
- Core models before outbox/consumer integration
- Story complete before moving to next priority for MVP incremental delivery

### Parallel Opportunities

- All Setup tasks marked [P] (T005) can run in parallel after T001
- All Foundational VOs/Enumerations marked [P] (T007-T011, T013, T015) can run in parallel within Phase 2 (different files)
- Foundational configs T014-T016 independent after T006, specs T018 parallel with events T012
- US1 domain aggregates T023 standalone, T024 consumer parallel, DTOs T025 parallel, outbox enricher T026 parallel
- US1 tests T020-T022 parallel (different test files: CatalogCompleteness vs ConsumerIdempotency vs CorrelationPropagation)
- Once Foundational completes, US1 and US2 immutability domain tests can be staffed in parallel by different devs (US2 T027-T029 reflection tests do not touch US1 T024 consumer except shared AuditEntry)
- Different user stories can be staffed in parallel after US1 (e.g., Dev A: US2, Dev B: US3 filtered search, Dev C: US4 health) — coordination via AuditEntry table (append-only, no write conflict except tail lock if hash chaining)
- Polish tasks marked [P] (T051-T055, T057) can run in parallel (different files: tracing vs pagination vs quickstart script)

---

## Parallel Example: User Story 1 (Append-only trail)

```bash
# Domain + consumer in parallel (different files):
Task: "Create AuditEntry aggregate in src/Modules/Audit/Audit.Domain/Aggregates/AuditEntry.cs" (T023)
Task: "Implement AuditEventConsumer in src/Modules/Audit/Audit.Infrastructure/Consumers/AuditEventConsumer.cs" (T024)
Task: "Create Audit contracts DTOs in src/Modules/Audit/Audit.Contracts/DTOs/AuditDtos.cs" (T025)

# Tests in parallel (different test files):
Task: "CatalogCompleteness test in tests/Audit.Tests/Unit/CatalogCompletenessTests.cs" (T020)
Task: "ConsumerIdempotency test in tests/Audit.Tests/Integration/ConsumerIdempotencyTests.cs" (T021)
Task: "CorrelationPropagation test in tests/Audit.Tests/Integration/CorrelationPropagationTests.cs" (T022)
```

## Parallel Example: User Story 3 (Filtered search)

```bash
# Policy pure + infrastructure adapter parallel (different files):
Task: "Create IAuditQueryAuthorization pure domain in src/Modules/Audit/Audit.Domain/Services/AuditQueryAuthorization.cs" (T039)
Task: "Create AuditQueryAuthorizationService infra adapter in src/Modules/Audit/Audit.Infrastructure/Services/AuditQueryAuthorizationService.cs" (T040)
Task: "Unit test QueryAuthorization composition in tests/Audit.Tests/Unit/QueryAuthorizationTests.cs" (T035)

# Query handlers parallel (different files):
Task: "SearchAuditEntriesQuery in src/Modules/Audit/Audit.Application/Features/Search/SearchAuditEntriesQuery.cs" (T041)
Task: "GetAuditTrailQuery in src/Modules/Audit/Audit.Application/Features/Trail/GetAuditTrailQuery.cs" (T042)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only — Append-only trail as compliance MVP)

1. Complete Phase 1: Setup (T001-T005) — Audit module + OTel/CorrelationIdMiddleware + AuditOptions
2. Complete Phase 2: Foundational (T006-T019) — StronglyTypedId/AuditAction 31/VOs/Immutable AuditEntry + REVOKE/hash columns + AuditConsumedEvent dedup + CorrelationIdMiddleware
3. Complete Phase 3: User Story 1 (T020-T026) — AuditEntry aggregate + AuditEventConsumer idempotent + 22-action catalog + CorrelationId propagation via Activity.Baggage
4. **STOP and VALIDATE**: Perform R2 catalog completeness: one action per 22 types with common CorrelationId C1 → SearchAuditEntries filter CorrelationId=C1 returns 22 entries within 2s, each Actor/Resource/Result/CorrelationId correct, BeforeAfterSnapshot masked, duplicate EventId → zero second entry. No audit entry without actor/resource/correlation. Deploy/demo as compliance MVP (SC-001).

### Incremental Delivery (Full 4 stories — audit + monitoring)

1. Setup + Foundational → Foundation ready (T001-T019)
2. Add US1 → Test independently → Deploy/Demo (append-only trail, SC-001) — one AuditEntry per catalog action, CorrelationId propagated
3. Add US2 → Test independently → Deploy/Demo (+ immutability, SC-002) — zero public setters, REVOKE/VerifyChain, corrections new AuditCorrected
4. Add US3 → Test independently → Deploy/Demo (+ filtered search 8 dims + trail + timeline, SC-003/SC-004) — Golden Rule A pre-filter before fetch, cross-branch filtered out, 7-entry timeline ordered
5. Add US4 → Test independently → Deploy/Demo (+ monitoring, SC-005) — per-dependency health distinct postgres vs rabbitmq, /alive still Healthy, metrics QueueDepth/latency in Aspire dashboard
6. Polish → OTel tracing enrichment + pagination Link + quickstart-validation.sh → full 5 SCs green via `quickstart-validation.sh` (catalog completeness 22 actions, immutability reflection, filtered search cross-branch, timeline 7, health per dependency)

### Parallel Team Strategy

With 3 developers after Foundational:

- **Developer A**: US1 (append-only trail + consumer idempotency + CorrelationId) → US2 (immutability via reflection/REVOKE/VerifyChain + AuditCorrected)
- **Developer B**: US3 (filtered search 8 dims + Golden Rule A pre-filter + cross-branch security + timeline) — owns IAuditQueryAuthorization pure/testable, safe default filtered
- **Developer C**: US4 (health per dependency via AddHealthChecks + metrics + logs via AddServiceDefaults) — owns OTel/Metrics, reuses Verification of US1 audit trail as dual observability, independent of audit search

After US1-US3, join:
- **Developer A** + **Developer B**: Polish (OTel baggage enrichment + pagination Link + quickstart-validation.sh + EXPLAIN ANALYZE index verification + ADRs for hash chain/alerting)
- **Developer C**: continues US4 dashboard widgets (Aspire dashboard Metrics/Traces) + health liveness vs readiness split

Stories integrate via AuditEntry table (append-only, no write conflict except tail lock if hash chaining) + outbox topic audit.* + CorrelationId; no cross-story file conflicts except shared AuditEntry table (coordinate via AddAsync only).

---

## Notes

- [P] tasks = different files, no dependencies - safe to parallelize
- [Story] label maps task to user story for traceability (FR-001..018 coverage documented in task descriptions + contracts)
- Each user story independently completable and testable with its Independent Test criteria (one entry per catalog action within 2s, zero setters compiled, zero cross-branch, 7-entry timeline ordered, per-dependency HealthReport distinct)
- Verify tests fail before implementing (TDD per Constitution XXI — tests in each US phase are CatalogCompletenessTests, AuditEntryIsImmutableTests, CrossBranchAuditSearchTests, HealthPerDependencyTests)
- Commit after each task or logical group (e.g., T023-T025 domain+consumer batch, T039-T040 policy pure/infra batch)
- Stop at any checkpoint to validate story independently (quickstart.md pillars SC-001..005 map to story checkpoints 1-4)
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence, audit UPDATE/DELETE path compiled, global unfiltered audit index, audit search without tenant predicate, audit consumer without EventId dedup, BeforeAfterSnapshot without masking, health without per-dependency Entries
- Tenant isolation: every Specification includes tenant_id, cross-tenant returns 404 (not 403) per Principle XV; audit query authorization reuses Golden Rule A scoped to AuditEntry.OrganizationId snapshot at emission time
- Outbox: every business write (catalog action) uses same-transaction IOutboxWriter, CorrelationId propagates via Activity.Baggage + TenantContext.CorrelationId + IOutboxWriter.CorrelationId + IntegrationEvent.CorrelationId + AuditEntry.CorrelationId
- Observability: AddServiceDefaults() OTel flow, Activity.Baggage CorrelationId correlates HTTP→DomainEvent→IntegrationEvent→AuditEntry, Aspire dashboard Metrics/Traces/Logs, health per-dependency distinct, metrics http_requests_failed_total/job_failed_total/rabbitmq_queue_depth
- Masking: IAuditMaskingPolicy depth-first JsonDocument traversal for Audit:MaskedFields (ApiKey/Password/Secret→***), masked before persistence, never raw

