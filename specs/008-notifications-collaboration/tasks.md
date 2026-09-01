# Tasks: Notifications and Collaboration

**Input**: Design documents from `specs/008-notifications-collaboration/` (plan.md, spec.md, data-model.md, research.md, contracts/, quickstart.md)
**Prerequisites**: plan.md (tech stack .NET 10, BuildingBlocks, Npgsql, RabbitMQ), spec.md (6 user stories), data-model.md (2 aggregates + VOs), contracts/ (4 contracts)
**Tests**: Included — TDD required per constitution XX and spec TDD Strategy (unit: policy merge, dedupe, content safety; integration: consumer idempotency, channel fan-out, failure observability)
**Organization**: Tasks grouped by user story for independent implementation and testing

## Format: `[ID] [P?] [Story] Description`
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps to user story [US1]–[US6] from spec.md

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify module scaffolding and shared contracts exposure

- [x] T001 Verify Notifications module scaffolding exists per plan structure in src/Modules/Notifications/Notifications.Domain/Notifications.Domain.csproj, src/Modules/Notifications/Notifications.Application/Notifications.Application.csproj, src/Modules/Notifications/Notifications.Infrastructure/Notifications.Infrastructure.csproj, src/Modules/Notifications/Notifications.Contracts/Notifications.Contracts.csproj
- [x] T002 Verify Api composition wiring placeholder for NotificationsDbContext in src/Api/Program.cs (points to src/Modules/Notifications/Notifications.Infrastructure/Persistence/NotificationsDbContext.cs already scaffolded with HasDefaultSchema("notifications"))
- [x] T003 [P] Configure Notifications:MandatedTypes and pagination defaults in src/Api/appsettings.json and src/Modules/Notifications/Notifications.Infrastructure/Configuration/NotificationsOptions.cs (InApp mandated WorkItemOverdue/Blocked/RiskIncreased per research D4)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain primitives, persistence, and shared services that ALL user stories depend on — MUST complete before any story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create NotificationId StronglyTypedId in src/Modules/Notifications/Notifications.Domain/Ids/NotificationId.cs (record NotificationId(Guid Value): StronglyTypedId<Guid>(Value))
- [x] T005 [P] Create NotificationType enumeration (11 values WorkItemAssigned..RiskIncreased extensible) in src/Modules/Notifications/Notifications.Domain/Enumerations/NotificationType.cs (BuildingBlocks Enumeration, FromId/FromName, sealed)
- [x] T006 [P] Create Channel enumeration (InApp=1, Email=2 extensible) in src/Modules/Notifications/Notifications.Domain/Enumerations/Channel.cs
- [x] T007 [P] Create DeliveryState enumeration (Pending, Delivered, Failed, SkippedByPreference, SkippedByPolicy) in src/Modules/Notifications/Notifications.Domain/Enumerations/DeliveryState.cs
- [x] T008 [P] Create DedupeKey ValueObject (SourceEventId+RecipientId+ChannelId, equality by value, GetEqualityComponents) in src/Modules/Notifications/Notifications.Domain/ValueObjects/DedupeKey.cs
- [x] T009 [P] Create NotificationContent ValueObject (Title 1..200, Body 1..2000, Link 1..500, content-safety allowlist hook) in src/Modules/Notifications/Notifications.Domain/ValueObjects/NotificationContent.cs
- [x] T010 Create Notification aggregate root (NotificationId PK, RecipientId, TenantId, SourceEventId, SourceResourceId, NotificationTypeId, ChannelId, DeliveryStateId, Title/Body/Link safe, CreatedAt, ReadAt nullable, CorrelationId) with Create factory raising NotificationCreated and MarkRead method raising NotificationRead (idempotent) in src/Modules/Notifications/Notifications.Domain/Aggregates/Notification.cs
- [x] T011 Create NotificationPreference aggregate root (UserId PK, TenantId, PreferencesJson Dictionary<int,Dictionary<int,bool>>, UpdatedAt, RowVersion IsRowVersion) with Update method and PreferencesUpdated event in src/Modules/Notifications/Notifications.Domain/Aggregates/NotificationPreference.cs
- [x] T012 [P] Create domain events NotificationCreated, NotificationRead, PreferencesUpdated in src/Modules/Notifications/Notifications.Domain/Events/NotificationDomainEvents.cs (IDomainEvent, correlation propagation)
- [x] T013 [P] Create domain services contracts INotificationPolicy (ResolveRecipients, IsEnabled, MandatedTypes, DefaultPreferences), IChannelRouter/IChannel, INotificationContentPolicy (Compose) in src/Modules/Notifications/Notifications.Domain/Services/INotificationServices.cs
- [x] T014 [P] Create business rules DedupeKeyRequiredRule, ContentSafetyRule, PreferenceValidationRule in src/Modules/Notifications/Notifications.Domain/Rules/NotificationBusinessRules.cs (IBusinessRule via CheckRule)
- [x] T015 Configure NotificationsDbContext with HasDefaultSchema("notifications"), entity configs NotificationConfiguration (UNIQUE(SourceEventId,RecipientId,Channel), INDEX(RecipientId,CreatedAt desc), INDEX(RecipientId) WHERE ReadAt IS NULL, INDEX(CorrelationId)), NotificationPreferenceConfiguration (jsonb PreferencesJson, IsRowVersion), and OutboxEntityTypeConfiguration in src/Modules/Notifications/Notifications.Infrastructure/Persistence/Configurations/NotificationEntityConfigurations.cs and update src/Modules/Notifications/Notifications.Infrastructure/Persistence/NotificationsDbContext.cs
- [ ] T016 Create EF Core migration for notifications schema in src/Modules/Notifications/Notifications.Infrastructure/Persistence/Migrations/ (dotnet ef migrations add Notifications_008_Initial --context NotificationsDbContext --project src/Modules/Notifications/Notifications.Infrastructure)
- [x] T017 [P] Create shared DTOs NotificationResponse, PagedNotificationsResponse, UnreadCountResponse in src/Modules/Notifications/Notifications.Contracts/Dtos/NotificationDtos.cs (per notifications-api-contract.md)
- [x] T018 [P] Create shared DTOs PreferencesResponse, UpdatePreferencesRequest, MandatedTypeDto in src/Modules/Notifications/Notifications.Contracts/Dtos/PreferenceDtos.cs (per preferences-api-contract.md)
- [x] T019 [P] Create EF specifications NotificationByRecipientSpec, UnreadNotificationsSpec, NotificationByIdSpec, PreferenceByUserSpec in src/Modules/Notifications/Notifications.Infrastructure/Specifications/NotificationSpecifications.cs (Specification<T> with recipient-first predicate)

**Checkpoint**: Foundation ready — domain model, persistence, and specifications are compile-time verified; user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Receive In-App Notifications from Business Events (Priority: P1) 🎯 MVP

**Goal**: Assignee/owner/watcher receives one InApp notification per relevant integration event (WorkItemAssigned etc.) with safe metadata+link, isolated in BC-09 (R1)

**Independent Test**: Publish single WorkItemAssignedIntegrationEvent(WorkItemId, ProjectId, TenantId, AssigneeId) via IEventBus → GetMyNotifications for assignee shows exactly one unread with correct type and link; no notification logic in producer modules

### Tests for User Story 1 ⚠️ Write FIRST, ensure FAIL

- [ ] T020 [P] [US1] Unit test for Notification.Create invariants (DedupeKey required, Title/Body/Link required) in tests/Notifications.Tests/Unit/NotificationCreationTests.cs
- [ ] T021 [P] [US1] Unit test for INotificationPolicy recipient resolution WorkItemAssigned→AssigneeId in tests/Notifications.Tests/Unit/RecipientResolutionTests.cs
- [ ] T022 [P] [US1] Integration test single WorkItemAssigned → one InApp notification in tests/Notifications.Tests/Integration/SingleEventNotificationTests.cs (publish via IEventBus, assert GetMyNotifications count 1, title contains WorkItemId, link /projects/{pid}/work-items/{wid})
- [ ] T023 [P] [US1] Contract test GET /api/notifications structure (paged envelope, ordering desc) in tests/Notifications.Tests/Contract/GetMyNotificationsContractTests.cs

### Implementation for User Story 1

- [x] T024 [P] [US1] Implement INotificationPolicy.ResolveRecipients per event type (WorkItemAssigned→AssigneeId, WorkItemBlocked→Owner+Assignee via event fields, DocumentApproved→OwnerId, RiskIncreased→ProjectMembers stub) in src/Modules/Notifications/Notifications.Infrastructure/Services/NotificationPolicy.cs (pure, config-backed MandatedTypes empty for US1)
- [x] T025 [P] [US1] Implement INotificationContentPolicy.Compose allowlist for WorkItemAssigned/Blocked/Completed (title "You were assigned work item {id}", link /projects/{pid}/work-items/{id}) in src/Modules/Notifications/Notifications.Infrastructure/Services/NotificationContentPolicy.cs
- [x] T026 [US1] Implement NotificationDispatcher handler for WorkItemAssignedIntegrationEvent fan-out (resolveRecipients → for each recipient: IsEnabled check default true for InApp in US1 → compose content → attempt AddAsync+SaveChanges with 23505 swallow stub) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs
- [x] T027 [US1] Register dispatcher subscriptions for work events (WorkItemAssignedIntegrationEvent, WorkItemStatusChangedIntegrationEvent) via AddSubscription in src/Api/Program.cs AddRabbitMqEventBus wiring and add src/Modules/Notifications/Notifications.Infrastructure/Consumers/WorkEventHandlers.cs adapters
- [x] T028 [US1] Implement GetMyNotifications query/handler/endpoint (Query, Handler, Validator pagination, IEndpoint GET /api/notifications with recipient-only Specification + AsNoTracking + OrderByDescending CreatedAt) in src/Modules/Notifications/Notifications.Application/Features/GetMyNotifications/
- [x] T029 [US1] Implement GetUnreadCount query/handler/endpoint (Query, Handler, IEndpoint GET /api/notifications/unread-count with COUNT WHERE ReadAt IS NULL) in src/Modules/Notifications/Notifications.Application/Features/GetUnreadCount/
- [x] T030 [US1] Add correlation propagation dispatcher → Notification.CorrelationId via TenantContext/Activity.Baggage in src/Modules/Notifications/Notifications.Infrastructure/Consumers/CorrelationEnricher.cs (reuses 007 pattern)

**Checkpoint**: US1 fully functional — single event produces one InApp with safe link, idempotency not yet (US2), preferences not yet (US3)

---

## Phase 4: User Story 2 - Duplicate Events Do Not Create Duplicate Notifications (Priority: P1)

**Goal**: At-least-once redelivery of same event (same SourceEventId+RecipientId+Channel) never creates second notification (R3)

**Independent Test**: Publish same WorkItemAssignedIntegrationEvent(Id=evt-123) twice → GetMyNotifications stays at 1, 10× storm still 1, concurrent consumers race safely; two distinct events for same workItem create 2 notifications

### Tests for User Story 2 ⚠️

- [ ] T031 [P] [US2] Unit test DedupeKey equality (value equality, hash, channel distinctness) in tests/Notifications.Tests/Unit/DedupeKeyEqualityTests.cs
- [ ] T032 [P] [US2] Integration test duplicate delivery idempotency (publish evt-123 twice → count 1, no second NotificationCreated) in tests/Notifications.Tests/Integration/DispatcherIdempotencyTests.cs
- [ ] T033 [P] [US2] Integration test distinct events produce distinct notifications + per-recipient dedupe (evt-123+Alice vs evt-123+Bob → 2 rows, redelivery → 0 new) in tests/Notifications.Tests/Integration/PerRecipientDedupeTests.cs

### Implementation for User Story 2

- [x] T034 [US2] Harden NotificationsDbContext UNIQUE constraint (SourceEventId, RecipientId, Channel) with migration check in src/Modules/Notifications/Notifications.Infrastructure/Persistence/Configurations/NotificationEntityConfigurations.cs (ensure HasIndex(...).IsUnique() and tested)
- [x] T035 [US2] Enhance NotificationDispatcher to swallow Postgres 23505 unique violation as already-delivered success (catch DbUpdateException with InnerException Npgsql.PostgresException SqlState 23505 → return success without rethrow, log Duplicate deduped structured) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs
- [x] T036 [US2] Add deduplication log + deliveryState handling for duplicate path (no NotificationCreated on swallow) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs

**Checkpoint**: US1+US2 both work — burst redelivery produces 0 duplicates, distinct events still fan out

---

## Phase 5: User Story 3 - Manage Notification Preferences Within Organizational Policy (Priority: P2)

**Goal**: User configures type×channel matrix via UpdatePreferences, bounded by org policy mandates that override opt-out (R4), default all InApp enabled

**Independent Test**: Carol PUT /api/notifications/preferences {DocumentUploaded:{InApp:false}} → publish DocumentUploaded targeting Carol → 0 notifications; but RiskIncreased (mandated) still produces 1 despite Carol false; GET /preferences shows effective vs raw

### Tests for User Story 3 ⚠️

- [ ] T037 [P] [US3] Unit test policy merge matrix (mandated true overrides false, non-mandated false skips, unset → default InApp true Email false) in tests/Notifications.Tests/Unit/PolicyMergeTests.cs
- [ ] T038 [P] [US3] Unit test UpdatePreferences validation rejects unknown NotificationType/Channel in tests/Notifications.Tests/Unit/PreferenceValidationTests.cs
- [ ] T039 [P] [US3] Contract test PUT /api/notifications/preferences 200/400/409 and GET /api/notifications/preferences effective vs raw in tests/Notifications.Tests/Contract/PreferencesContractTests.cs
- [ ] T040 [P] [US3] Integration test preference disabled skips creation + mandated overrides in tests/Notifications.Tests/Integration/PreferenceFilteringTests.cs

### Implementation for User Story 3

- [x] T041 [P] [US3] Implement INotificationPolicy.IsEnabled merge logic (MandatedTypes.Contains → true else userPrefs else DefaultPreferences) and MandatedTypes from NotificationsOptions in src/Modules/Notifications/Notifications.Infrastructure/Services/NotificationPolicy.cs
- [x] T042 [US3] Implement NotificationPreference Update method with RowVersion bump and RaiseDomainEvent PreferencesUpdated in src/Modules/Notifications/Notifications.Domain/Aggregates/NotificationPreference.cs
- [x] T043 [US3] Implement UpdatePreferences command/handler/validator/endpoint (PUT /api/notifications/preferences, Validator rejects unknown enums 400, RowVersion check 409, SaveChanges with RaiseDomainEvent, return PreferencesResponse with raw+effective+mandated) in src/Modules/Notifications/Notifications.Application/Features/UpdatePreferences/
- [x] T044 [US3] Implement GetPreferences query/handler/endpoint (GET /api/notifications/preferences returns raw+effective+mandated, missing row → effective defaults) in src/Modules/Notifications/Notifications.Application/Features/GetPreferences/
- [x] T045 [US3] Wire INotificationPolicy.IsEnabled into NotificationDispatcher before insert (skip creation when IsEnabled false, log SkippedByPreference; but still deliver if mandated) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs

**Checkpoint**: US1-3 independent — preferences gate dispatcher, mandated still delivers

---

## Phase 6: User Story 4 - View, Count, and Mark Notifications as Read (Priority: P2)

**Goal**: User reviews paginated inbox newest-first, unread badge via GetUnreadCount, marks own notification as read idempotently; cross-user forbidden

**Independent Test**: Seed 3 unread+2 read → GET /api/notifications page 1 size 20 ordered desc + GET /api/notifications/unread-count 3 → POST /api/notifications/{id}/read → unread-count 2, second POST same id idempotent; Dave marking Eve's notification → 403; 50 notifications page 1 returns 20 with Link header

### Tests for User Story 4 ⚠️

- [ ] T046 [P] [US4] Contract test GET /api/notifications pagination and ordering (CreatedAt desc, Link header) in tests/Notifications.Tests/Contract/PaginationContractTests.cs
- [ ] T047 [P] [US4] Integration test GetUnreadCount with 10k seeded notifications (performance <500ms p95) in tests/Notifications.Tests/Integration/UnreadCountPerformanceTests.cs
- [ ] T048 [P] [US4] Integration test MarkRead idempotency (second call wasAlreadyRead true, no duplicate NotificationRead) in tests/Notifications.Tests/Integration/MarkReadIdempotencyTests.cs
- [ ] T049 [P] [US4] Security test MarkRead cross-user 403 and GetMyNotifications cross-user returns 0 (private inbox) in tests/Notifications.Tests/Integration/CrossUserIsolationTests.cs

### Implementation for User Story 4

- [x] T050 [P] [US4] Enhance GetMyNotifications handler with optional filters unreadOnly and type (Specification composition + pagination validation 1..100) in src/Modules/Notifications/Notifications.Application/Features/GetMyNotifications/GetMyNotificationsHandler.cs
- [x] T051 [P] [US4] Implement MarkRead command/handler/validator/endpoint (POST /api/notifications/{id}/read, load tracked NotificationByIdSpec, check RecipientId==callerId else 403, tenant shadow 404 if TenantId mismatch, if ReadAt==null set ReadAt=UtcNow and RaiseDomainEvent NotificationRead, else idempotent) in src/Modules/Notifications/Notifications.Application/Features/MarkRead/
- [x] T052 [US4] Wire Api Program.cs RabbitMqEventBus no-op for read paths (ensure endpoints mapped via AddEndpoints typeof(Program).Assembly picks up Notifications.Application slices) in src/Api/Program.cs
- [x] T053 [US4] Add Result→HTTP mappings for MarkRead (400 validation, 403 Forbidden, 404 shadow) in src/Modules/Notifications/Notifications.Application/Features/MarkRead/MarkReadEndpoint.cs via result.ToResult() extensions

**Checkpoint**: US1-4 fully functional — inbox query, count, and mark-read are private and performant

---

## Phase 7: User Story 5 - Channel Decoupling and Extensibility (Priority: P2)

**Goal**: Dispatcher fans each event to IChannel implementations via IChannelRouter; InApp guaranteed, Email future stub; failure in one channel never blocks the other and is observable

**Independent Test**: Configure EmailChannel to throw → publish WorkItemCompleted → InApp still Delivered and queryable, Email DeliveryState=Failed row or log observable; add TestChannel via DI without modifying dispatcher → new channel receives without regression

### Tests for User Story 5 ⚠️

- [ ] T054 [P] [US5] Integration test channel fan-out InApp success + Email failure observability (Email throws → InApp still Delivered, log contains DeliveryState Failed) in tests/Notifications.Tests/Integration/ChannelFanOutTests.cs
- [ ] T055 [P] [US5] Integration test new TestChannel extensibility (register IChannel stub → fan-out without dispatcher change) in tests/Notifications.Tests/Integration/ChannelExtensibilityTests.cs

### Implementation for User Story 5

- [x] T056 [P] [US5] Create IChannel and IChannelRouter contracts plus ChannelRouter FanOutAsync loop try/catch per channel in src/Modules/Notifications/Notifications.Domain/Services/IChannelRouter.cs and src/Modules/Notifications/Notifications.Infrastructure/Channels/ChannelRouter.cs
- [x] T057 [P] [US5] Implement InAppChannel (Channel.InApp, DeliverAsync ensures Notification row Delivered, no external I/O) in src/Modules/Notifications/Notifications.Infrastructure/Channels/InAppChannel.cs
- [x] T058 [P] [US5] Implement EmailChannel stub (Channel.Email, DeliverAsync logs "Would send email to {RecipientId} title={Title} link={Link}" and returns Result.Success; configurable ShouldFail flag for tests throws) in src/Modules/Notifications/Notifications.Infrastructure/Channels/EmailChannel.cs (future SMTP/Graph provider note)
- [x] T059 [US5] Refactor NotificationDispatcher to create one Notification row per enabledChannel per recipient (per-channel dedupe rows) and call ChannelRouter.FanOutAsync with per-channel failure isolation (update DeliveryState Failed on catch without rolling back other channels) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs
- [x] T060 [US5] Register channels and router in DI (services.AddSingleton<IChannel, InAppChannel>(), AddSingleton<IChannel, EmailChannel>(), AddScoped<IChannelRouter, ChannelRouter>()) in src/Modules/Notifications/Notifications.Infrastructure/DependencyInjection/NotificationsServiceExtensions.cs

**Checkpoint**: US1-5 complete — Email failure never silences InApp, new channels plug via DI

---

## Phase 8: User Story 6 - Sensitive Content Never Leaks Into Notifications (Priority: P3)

**Goal**: Notifications for classified documents/AI payloads contain only metadata+link, never document body or AI result payload (R5), per-type allowlist

**Independent Test**: Trigger DocumentApproved with Confidential classification and AiReviewRequested with 50KB payload → inspect Notification Title/Body/Link for each of 11 types → assert zero bytes of body/payload, only identifiers and links

### Tests for User Story 6 ⚠️

- [ ] T061 [P] [US6] Unit test content safety per NotificationType allowlist (11 types, Confidential body not contained, AI payload not contained) in tests/Notifications.Tests/Unit/ContentSafetyTests.cs
- [ ] T062 [P] [US6] Integration test Confidential document and AI payload leak zero bytes in tests/Notifications.Tests/Integration/ContentSafetyIntegrationTests.cs

### Implementation for User Story 6

- [x] T063 [P] [US6] Harden INotificationContentPolicy.Compose per-type allowlist (DocumentApproved: only DocumentId + Classification label + link, AiReviewRequested: only ResultId/OperationType label + DocumentId + link, ignore unknown future body field) in src/Modules/Notifications/Notifications.Infrastructure/Services/NotificationContentPolicy.cs
- [x] T064 [US6] Extend dispatcher to pass Classification/Provider fields safely without body enrichment (consume only event-carried safe fields, never fetch Document body from storage) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/NotificationDispatcher.cs

**Checkpoint**: All 6 stories independent — R5 verified across all 11 types

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Production hardening, performance, and validation

- [ ] T065 [P] Add performance composite indexes verification script and benchmark for GetMyNotifications/GetUnreadCount with 10k rows (<500ms p95) in src/Modules/Notifications/Notifications.Infrastructure/Persistence/Migrations/ (EXPLAIN ANALYZE note in quickstart)
- [ ] T066 [P] Add architecture test NotificationsOnlyConsumesEvents + NoCrossModuleDbContext + InboxQueriesRequireRecipientFilter in tests/Architecture/NotificationsBoundaryTests.cs (NetArchTest)
- [ ] T067 [P] Wire Notifications audit forwarding (NotificationCreated/Read + PreferencesUpdated → AuditEventConsumer via outbox) in src/Modules/Notifications/Notifications.Infrastructure/Consumers/AuditForwarding.cs or verify AppDbContextBase domain dispatch already covers it
- [ ] T068 [P] Create Web inbox components (list with pagination, unread badge, mark-read, preferences matrix with mandated-disabled toggles) per minimal-ui-design-system + ngrx-signal-store in src/Web/src/app/features/notifications/ (scaffold only, read contracts) — optional polish
- [ ] T069 Run quickstart.md validation (scenarios A–F) via tests or curl flow and ensure 0% duplicate under 10× redelivery and 0 bytes leakage across 11 types in tests/Notifications.Tests/Integration/QuickstartValidationTests.cs
- [ ] T070 Update specs/008-notifications-collaboration/checklists/requirements.md with polish verification and docs/ADR stub for channel choice (ADR-008-03) if needed

---

## Dependencies & Execution Order

### Phase Dependencies
- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational (Phase 2) completion
  - US1 (P1) and US2 (P1) are co-MVP but US2 hardens US1's dedupe; implement US1 then US2 sequentially or in parallel with care (shared dispatcher)
  - US3 (P2 preferences) may start after Foundational, but its dispatcher integration (T045) touches same file as US1/US2 — sequence US1→US2→US3 to avoid merge conflicts, or parallel with coordination
  - US4 (P2 inbox) and US5 (P2 channels) and US6 (P3 safety) can start in parallel after Foundational, but US6's content policy is part of US1's compose — implement US1's compose first, then harden in US6
  - Polish (Phase 9) depends on all desired stories being complete

### User Story Dependencies
- **US1 (P1) Receive In-App**: Can start after Foundational — no dependencies on other stories; delivers core value
- **US2 (P1) Dedupe**: Depends on US1's dispatcher and UNIQUE constraint — technically enhances US1; can be merged into US1 but kept separate for idempotency focus
- **US3 (P2) Preferences**: Depends on Foundational (NotificationPreference aggregate) — independent of US1/US2 dispatch logic but integrates at T045
- **US4 (P2) Inbox Queries**: Depends on Foundational (Notification) + US1 (rows exist) — but handler is independently testable with seeded rows
- **US5 (P2) Channels**: Depends on Foundational + US1 dispatcher — adds fan-out without modifying US1's InApp path
- **US6 (P3) Content Safety**: Depends on US1's content policy — hardens allowlist for all 11 types

### Within Each User Story
- Tests MUST be written and FAIL before implementation (TDD, constitution XXI)
- Enumerations/VOs before aggregates
- Aggregates before services
- Services before handlers
- Handlers before endpoints
- Core implementation before integration and dedupe handling
- Story complete before moving to next priority

### Parallel Opportunities
- All T005–T009 ([P] enumerations/VOs) can run in parallel in Phase 2
- T017–T019 ([P] DTOs/specifications) can run in parallel in Phase 2
- All contract/permission tests per story marked [P] can run in parallel (different test files)
- T024 and T025 ([P] policy resolvers and content policy) can run in parallel in US1
- T056–T058 ([P] channel contracts and implementations) can run in parallel in US5
- T061 ([P] content safety unit) and US4 tests can run in parallel by different team members once Foundational is done
- Different user stories can be worked on in parallel by different team members after Foundational checkpoint, with coordination on shared dispatcher file

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (must fail before impl):
dotnet test tests/Notifications.Tests/Unit/NotificationCreationTests.cs tests/Notifications.Tests/Unit/RecipientResolutionTests.cs tests/Notifications.Tests/Integration/SingleEventNotificationTests.cs tests/Notifications.Tests/Contract/GetMyNotificationsContractTests.cs --filter US1

# Launch all US1 foundational implementations together:
# T024 INotificationPolicy.ResolveRecipients + T025 INotificationContentPolicy.Compose run in parallel (different files)
```

## Parallel Example: Foundational

```bash
# All enumerations/VOs in parallel (no dependencies):
dotnet new ... # T005 Channel, T006 DeliveryState, T007 DedupeKey, T008 NotificationContent — four files, four parallel agents
```

---

## Implementation Strategy

### MVP First (User Stories 1+2 Only)

1. Complete Phase 1: Setup (T001–T003)
2. Complete Phase 2: Foundational (T004–T019) — CRITICAL blocks all stories
3. Complete Phase 3: US1 Receive In-App (T020–T030) — single event → one InApp with link
4. Complete Phase 4: US2 Dedupe (T031–T036) — 23505 swallow, 10× storm still 1
5. **STOP and VALIDATE**: Run quickstart scenarios A–B + dotnet test --filter US1|US2; verify SC-001/SC-002; demo inbox for Alice
6. Deploy/demo if ready — MVP delivers decoupled InApp notifications with idempotency

### Incremental Delivery

1. Setup + Foundational → Foundation ready (migrations, aggregates, specs)
2. Add US1 → Test independently → Deploy/Demo (MVP! — event → InApp)
3. Add US2 → Test independently → Deploy/Demo (no duplicates under at-least-once)
4. Add US3 → Test independently → Deploy/Demo (preferences gate, mandated overrides)
5. Add US4 → Test independently → Deploy/Demo (paginated inbox, unread badge, mark-read)
6. Add US5 → Test independently → Deploy/Demo (Email failure never blocks InApp, extensible)
7. Add US6 → Test independently → Deploy/Demo (0 bytes leakage across 11 types)
8. Polish (Phase 9) → architecture tests, perf indexes, audit forwarding, Web inbox
9. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers after Foundational checkpoint:

1. Team completes Setup + Foundational together (single developer or pair)
2. Once Foundational is done:
   - Developer A: US1 (dispatcher) + US2 (dedupe) — same dispatcher file, sequence them
   - Developer B: US3 (preferences) + US4 (inbox queries) — different aggregates/handlers, parallel
   - Developer C: US5 (channels) + US6 (content safety) — different services, parallel with A/B after initial dispatcher
3. Stories complete and integrate independently; final polish merges and validates cross-cutting (performance, architecture boundary, audit)

---

## Notes

- [P] tasks = different files, no dependencies — safe for parallel agents
- [Story] label maps task to specific user story for traceability (US1=InApp events, US2=Dedupe, US3=Preferences, US4=Inbox, US5=Channels, US6=ContentSafety)
- Each user story is independently completable and testable per its Independent Test in spec.md
- Verify tests fail before implementing (TDD, constitution XXI — BuildingBlocks aggregates/rules before handlers)
- Commit after each task or logical group (e.g., after T004–T009 enumerations)
- Stop at any checkpoint to validate story independently (quickstart scenarios A–F)
- Avoid: vague tasks, same-file conflicts without coordination (dispatcher file shared by US1/US2/US3/US5/US6), cross-story dependencies that break independence (Inbox queries must work with seeded rows without dispatcher)
- Constitutes traceability: Principles XVII (async idempotent via outbox+UNIQUE), XIX (content safety + recipient-only auth), V (BC-09 isolated), XXI (TDD+DDD+VerticalSlices via BuildingBlocks)
