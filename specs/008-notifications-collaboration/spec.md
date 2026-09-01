# Feature Specification: Notifications and Collaboration

**Feature Branch**: `008-notifications-collaboration`

**Created**: 2026-09-01

**Status**: Draft

**Input**: User description: "SPEC-008 — Notifications and Collaboration — Bounded Context: BC-09 Notifications (Supporting) · Depends on: SPEC-003, SPEC-005, SPEC-006 (event sources) — Objective: Notify users about changes requiring attention, decoupled from business events and channels. Requirements R1-R5, Domain Model, Application Layer, Acceptance Criteria, TDD Strategy as provided."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receive In-App Notifications from Business Events (Priority: P1)

A user (assignee, owner, reviewer, watcher) receives a timely in-app notification when a relevant business event occurs in another bounded context — e.g., work item assigned/reassigned, overdue, blocked, completed, review requested, document uploaded/classified/approved, AI review requested, or risk increased.

**Why this priority**: Core value of the feature. Without event-driven generation, users miss changes requiring attention. This is the sole notification trigger path (R1) and maps directly to acceptance criterion "WorkItemAssigned → assignee receives exactly one in-app notification."

**Independent Test**: Publish a single `WorkItemAssigned` integration event with a known assignee; query the assignee's inbox via `GetMyNotifications` and verify exactly one unread notification appears with correct type, reference link, and without business logic duplication in the source context.

**Acceptance Scenarios**:

1. **Given** a work item exists and user Alice is the assignee, **When** a `WorkItemAssigned` integration event for that work item is published, **Then** Alice's `GetMyNotifications` returns one new unread notification of type `WorkItemAssigned` with work-item metadata and a link (no duplicate).
2. **Given** a document is approved with classification `Confidential`, **When** a `DocumentApproved` integration event is published, **Then** stakeholders receive a notification containing only metadata/title/link — no document body or classified payload.
3. **Given** a risk score increases for a project Alice watches, **When** a `RiskIncreased` integration event is published, **Then** Alice receives one `RiskIncreased` notification with severity metadata and a link to the risk detail.
4. **Given** a work item transitions to `Blocked`, **When** a `WorkItemBlocked` integration event is published, **Then** the owner and assignee each receive a notification if policy designates them as recipients.

---

### User Story 2 - Duplicate Events Do Not Create Duplicate Notifications (Priority: P1)

The system guarantees at-least-once delivery handling: redelivery of the same integration event (same event ID + same recipient) never creates a second notification.

**Why this priority**: Messaging infrastructure is at-least-once; without deduplication users are spammed and trust erodes. Directly validates R3 and acceptance criterion on dedupe.

**Independent Test**: Publish the same integration event twice with identical event ID; query notifications before and after second delivery and assert count is unchanged and no new `NotificationCreated` domain event is emitted on the second consume.

**Acceptance Scenarios**:

1. **Given** a `WorkItemAssigned` event `evt-123` has already produced a notification for Bob, **When** `evt-123` is redelivered, **Then** Bob's notification count stays at one and no second notification row is created.
2. **Given** two distinct events `evt-1` and `evt-2` for the same work item and recipient, **When** both are consumed, **Then** two separate notifications exist (dedupe key is event ID + recipient, not work-item ID).
3. **Given** one event targeting two recipients (Alice and Bob), **When** consumed, **Then** two notifications exist with distinct dedupe keys (`evt-123+Alice`, `evt-123+Bob`); redelivery creates zero new notifications.

---

### User Story 3 - Manage Notification Preferences Within Organizational Policy (Priority: P2)

A user configures per-event-type × channel preferences (e.g., disable email for `DocumentUploaded`, keep InApp for `WorkItemBlocked`), bounded by organizational policy that may mandate certain notifications cannot be disabled.

**Why this priority**: Required for R4; prevents notification fatigue while ensuring compliance. Without it, every event generates noise or critical alerts can be accidentally muted.

**Independent Test**: As a user, call `UpdatePreferences` to disable InApp for `DocumentUploaded`; publish that event and verify no notification is created for that user, while publishing a policy-mandated type (e.g., `RiskIncreased`) still creates a notification despite the preference.

**Acceptance Scenarios**:

1. **Given** user Carol has disabled InApp for `DocumentUploaded`, **When** a `DocumentUploaded` event targeting Carol is consumed, **Then** no InApp notification is created for Carol.
2. **Given** organizational policy mandates delivery for `RiskIncreased` on InApp, **When** Carol has disabled that type, **Then** consuming `RiskIncreased` still creates an InApp notification for Carol (policy overrides preference).
3. **Given** Carol updates preferences via `UpdatePreferences`, **When** the command succeeds, **Then** a `PreferencesUpdated` domain event is recorded and subsequent `GetMyNotifications` behavior reflects the new preferences.

---

### User Story 4 - View, Count, and Mark Notifications as Read (Priority: P2)

A user reviews their notification inbox, sees an unread count badge, paginated history, and marks individual notifications as read. Unread state is personal and auditable.

**Why this priority**: Notifications have no value if they cannot be consumed. Covers the Application Layer queries `GetMyNotifications`/`GetUnreadCount` and command `MarkRead`.

**Independent Test**: Seed 3 unread + 2 read notifications for a user; call `GetMyNotifications` and `GetUnreadCount` and verify counts, ordering (newest first), and that `MarkRead` transitions one notification to read and emits `NotificationRead`.

**Acceptance Scenarios**:

1. **Given** user Dave has 5 unread notifications, **When** he calls `GetUnreadCount`, **Then** the result is 5; **When** he calls `MarkRead` on one, **Then** unread count becomes 4 and that notification's read timestamp is set.
2. **Given** Dave is not the owner of a notification (belongs to Eve), **When** Dave attempts `MarkRead` on Eve's notification, **Then** the operation is rejected (no cross-user mutation).
3. **Given** Dave has 50 notifications, **When** he pages `GetMyNotifications` (page 1, size 20), **Then** results are ordered by creation time descending with pagination metadata.

---

### User Story 5 - Channel Decoupling and Extensibility (Priority: P2)

The notification dispatcher fans each event out to subscribed channels via a channel abstraction. InApp is the default guaranteed channel; Email is future/extensible. Failure in one channel never blocks another and failures are observable.

**Why this priority**: Validates R2 and acceptance criteria "disabled email still delivers in-app; email failures are observable." Without decoupling, a broken email provider would silence all notifications.

**Independent Test**: Configure the Email channel to throw/fail; publish a single event targeting a user subscribed to both InApp and Email; verify InApp notification still appears, Email failure is recorded/observable (dead-letter or telemetry), and retry does not duplicate InApp.

**Acceptance Scenarios**:

1. **Given** Email channel is disabled or throws on delivery, **When** a `WorkItemCompleted` event is consumed, **Then** an InApp notification is created successfully and the Email failure is observable via logs/dead-letter visibility.
2. **Given** a new channel implementation subscribes to notification integration events, **When** an event flows, **Then** the new channel receives the event without changes to core dispatcher or other channels.
3. **Given** Email channel succeeds after a transient failure, **When** retried, **Then** deduplication still prevents duplicate Email deliveries (idempotent channel consumer).

---

### User Story 6 - Sensitive Content Never Leaks Into Notifications (Priority: P3)

Notifications for sensitive sources (classified documents, AI result payloads) contain only safe metadata and links; payloads are never embedded.

**Why this priority**: R5 — content safety is a compliance requirement. A single leak of classified content via notification is a security incident.

**Independent Test**: Trigger notifications for (a) `DocumentApproved` with `Confidential` classification, (b) `AiReviewRequested` with large result payload; inspect notification title/body/link fields and assert no document body bytes, no AI result JSON, only identifiers/titles/links.

**Acceptance Scenarios**:

1. **Given** a `Confidential` document approval event, **When** the notification is composed, **Then** title/body contain document name, classification label, and a link — zero bytes of document content.
2. **Given** an AI review event with result payload of 50KB, **When** the notification is composed, **Then** it contains operation ID, status, and link to review — no result payload.
3. **Given** a non-sensitive event (e.g., `WorkItemCompleted` with public title), **When** composed, **Then** title includes work-item title and link, still without internal payload beyond safe metadata.

---

### Edge Cases

- Duplicate event storm: 10 rapid redeliveries of same event ID + recipient — system creates exactly one notification; concurrent consumers race safely (unique constraint on dedupe key).
- Partial recipient failure: event targets 5 users; preference/policy evaluation for one user throws — other 4 users' notifications still created, failure is logged per-recipient.
- Preference for unknown event type: user sends `UpdatePreferences` with an unrecognized `NotificationType` — request fails validation; no partial update.
- Channel exception during fan-out: InApp succeeds, Email throws — dispatcher commits InApp delivery transactionally, records Email failure for observability without rolling back InApp.
- MarkRead idempotency: marking an already-read notification as read again succeeds idempotently; no duplicate `NotificationRead` event.
- Orphan event: integration event references a work item / document the recipient can no longer access (deleted or permission revoked) — dispatcher still creates metadata-only notification if policy says so; authorization for the link target is enforced at navigation time, not notification creation.
- Preference defaults: new user with no explicit preferences gets organization-default preferences (all InApp enabled unless policy says otherwise).
- Content safety regression: future event type adds a `body` field — notification template must not blindly include it; content-safety rule must be extended and test fails until safe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST derive notifications ONLY from integration events published by other bounded contexts (work items, documents, AI, risk/planning). No notification generation logic may reside inside those contexts; all mapping from business events to notifications SHALL be isolated in BC-09 Notifications.
- **FR-002**: System MUST handle at minimum the following integration event types as notification triggers: `WorkItemAssigned`, `WorkItemReassigned`, `WorkItemOverdue`, `WorkItemBlocked`, `WorkItemCompleted`, `ReviewRequested`, `DocumentUploaded`, `DocumentClassified`, `DocumentApproved`, `AiReviewRequested`, `RiskIncreased`. Each maps to a distinct `NotificationType` enumeration value. Additional types may be added without breaking existing behavior.
- **FR-003**: For each consumed integration event, system MUST determine recipients via `INotificationPolicy` (e.g., assignee for assigned, owner+assignee for blocked, configured watchers for risk) — never hard-coded outside the policy.
- **FR-004**: System MUST generate one `Notification` aggregate per recipient per event (fan-out). Each notification has identity `NotificationId`, recipient `UserId`, `NotificationType`, safe title/body, deep link to source resource, `DeliveryState` per channel, creation timestamp, and read timestamp (nullable).
- **FR-005**: System MUST enforce content safety at composition time: notifications SHALL contain only metadata and links (identifiers, titles, classification labels, status) and SHALL NOT embed document bodies, classified content, or AI result payloads. Composition rules must be unit-tested per `NotificationType`.
- **FR-006**: System MUST deduplicate on `DedupeKey = eventId + recipientId` (plus optionally `Channel` when per-channel dedupe is needed). Duplicate delivery of the same event for the same recipient SHALL NOT create a second notification; consumer handlers MUST be idempotent under at-least-once delivery. Uniqueness SHALL be enforced at the persistence layer.
- **FR-007**: System MUST support channel abstraction with at minimum `InApp` (default, guaranteed) and `Email` (future, optional). Channel implementations SHALL subscribe to notification integration events independently; failure or unavailability of one channel SHALL NOT block delivery to other channels.
- **FR-008**: System MUST fan out via `IChannelRouter` / dispatcher: `NotificationDispatcher` consumes integration events → resolves recipients via `INotificationPolicy` → applies preference/policy merge → routes to each enabled channel. Each channel consumer is independently retryable and observable.
- **FR-009**: System MUST make channel failures observable: failed deliveries SHALL be logged with structured telemetry, expose dead-letter/failed-delivery visibility, and be available for operational inspection without exposing sensitive content. Retry behavior (exponential backoff, max attempts) SHALL be defined but not required to be user-visible.
- **FR-010**: System MUST support per-user `NotificationPreference` aggregate (root per `UserId`) storing a matrix of `NotificationType × Channel → enabled (boolean)`. Preferences SHALL be updatable via `UpdatePreferences` command and emit `PreferencesUpdated` domain event. Reads are via queries; writes are audited.
- **FR-011**: System MUST merge preferences with organizational policy via `INotificationPolicy`: if policy mandates delivery for a given `NotificationType × Channel` (e.g., `RiskIncreased` on `InApp`), preference cannot suppress it. Evaluation order: policy mandates override user opt-out; otherwise preference controls. Default for new users is policy-default (typically all InApp enabled).
- **FR-012**: System MUST provide query `GetMyNotifications` — returns current user's notifications paginated, filtered (read/unread, type), ordered newest-first, authorization-filtered to recipient-only. Must NOT return other users' notifications.
- **FR-013**: System MUST provide query `GetUnreadCount` — returns count of unread notifications for the current user, efficiently (no full scan required for typical scale).
- **FR-014**: System MUST provide command `MarkRead` — transitions a single notification from unread to read for its owner, emits `NotificationRead`, sets read timestamp; idempotent if already read; authorization SHALL reject cross-user marks.
- **FR-015**: System MUST emit domain events `NotificationCreated` on creation and `NotificationRead` on mark-read. Aggregates SHALL enforce invariants: a notification cannot be created without a valid dedupe key; cannot be marked read twice with duplicate domain events.
- **FR-016**: System MUST audit preference changes and notification reads where required by Principles VI/VIII (at minimum `PreferencesUpdated` and `NotificationRead` are auditable; `NotificationCreated` is traceable via domain event + telemetry).
- **FR-017**: System SHALL persist notifications and preferences with tenant/organization awareness where applicable (recipient lookup respects organizational scope per Principle XV), but notifications themselves are user-scoped.
- **FR-018**: System MUST support bulk cleanup/retention policy for notifications (e.g., configurable retention window) — not required in v1 to auto-delete, but model SHALL include creation timestamp enabling future retention without schema change. [Assumption documented below]
- **FR-019**: System MUST validate all commands/queries (FluentValidation-style): `UpdatePreferences` rejects unknown notification types/channels; `MarkRead` validates identifier format; dispatcher validates event payload presence (missing eventId or recipient yields dead-letter, not silent drop).

### Key Entities

- **Notification** (Aggregate Root, `NotificationId` : StronglyTypedId): Integration-event-derived inbox item. Attributes: `RecipientId` (UserId), `DedupeKey` (eventId + recipientId), `NotificationType` (Enumeration: WorkItemAssigned, WorkItemReassigned, WorkItemOverdue, WorkItemBlocked, WorkItemCompleted, ReviewRequested, DocumentUploaded, DocumentClassified, DocumentApproved, AiReviewRequested, RiskIncreased, ... extensible), `Channel` (Enumeration: InApp, Email, ...), `Title` (safe, metadata-only), `Body` (safe, metadata + link text), `Link` (deep link to source resource, e.g., `/work-items/{id}`), `DeliveryState` (Enumeration: Pending, Delivered, Failed), `CreatedAt`, `ReadAt` (nullable), `SourceEventId`, `SourceResourceId`. Emits `NotificationCreated`, `NotificationRead`.
- **NotificationPreference** (Aggregate Root, per `UserId`): User's opt-in/out matrix. Attributes: `UserId`, `Preferences` (map `NotificationType × Channel → enabled`), `UpdatedAt`, `Version` (for optimistic concurrency). Emits `PreferencesUpdated`. Invariants: channel-enabled map must reference valid enumerations; policy-mandated types cannot be suppressed at evaluation time (enforced at read-time merge, not storage).
- **DedupeKey** (Value Object): Composite `EventId + RecipientId (+ Channel when per-channel dedupe)`. Equality by value; used for idempotency. Persistence has unique constraint on this key.
- **Channel** (Enumeration Value Object): `InApp`, `Email`, extensible. Behavior: each channel defines its delivery contract; `IChannelRouter` resolves enabled channels per notification.
- **NotificationType** (Enumeration Value Object): Maps 1:1 to integration event types; extensible without migration.
- **DeliveryState** (Enumeration Value Object): `Pending`, `Delivered`, `Failed`, `SkippedByPreference`, `SkippedByPolicy` (optional for observability).
- **Integration Events (consumed, not owned)**: `WorkItemAssigned`, `WorkItemReassigned`, `WorkItemOverdue`, `WorkItemBlocked`, `WorkItemCompleted`, `ReviewRequested`, `DocumentUploaded`, `DocumentClassified`, `DocumentApproved`, `AiReviewRequested`, `RiskIncreased`. Source contexts: Projects/Work (SPEC-003), Documents (SPEC-005), LLM Intelligence (SPEC-006), Planning/Metrics. The Notifications context never produces business integration events about work/documents — it only consumes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any consumed `WorkItemAssigned` event, the designated assignee sees the corresponding in-app notification in `GetMyNotifications` within 5 seconds of event publish in 95% of deliveries under normal load (measured end-to-end via integration test harness).
- **SC-002**: Redelivery of the same integration event (same event ID) 10 times causes zero duplicate notifications for the same recipient — duplicate rate is 0% (verified by uniqueness constraint + integration test with at-least-once simulation).
- **SC-003**: With the Email channel intentionally disabled/failing, 100% of in-app notifications for a burst of 50 events are still delivered; email failures are 100% observable in structured logs/dead-letter inspection (no silent drops).
- **SC-004**: Notifications for `Confidential` document approvals and AI review events contain zero bytes of document body or AI payload when inspected across all notification types — content-safety check passes for 100% of type-specific templates (unit-tested per type).
- **SC-005**: A user who disables a non-mandated notification type (e.g., `DocumentUploaded` on InApp) receives 0 notifications for that type, while a policy-mandated type (e.g., `RiskIncreased` on InApp) still generates 100% of expected notifications despite opt-out (preference × policy merge verified).
- **SC-006**: `GetUnreadCount` and `MarkRead` complete within 500 ms at p95 for a user with 10,000 notifications, and `GetMyNotifications` paginated query (page size 20) completes within 500 ms at p95.
- **SC-007**: New channel implementations can subscribe to notification events and receive deliveries without modification to existing channel code — verified by adding a stub/test channel in integration test and observing fan-out without regression in InApp delivery.
- **SC-008**: 90% of users can locate and act on a notification (open linked resource) within 30 seconds of seeing the unread badge in usability walkthrough; mark-read success rate is 95% on first attempt.

## Assumptions

- Event sources are existing integration events from SPEC-003 (Projects/Work Kanban), SPEC-005 (Document Management), and SPEC-006 (LLM Document Intelligence). If an event type from those specs is renamed, Notifications mapping follows the new name without requiring spec amendment — the enumeration is extensible.
- `InApp` delivery is via persisted inbox polled through `GetMyNotifications`/`GetUnreadCount` (and optionally real-time push in a later slice). This spec does not assume WebSocket/SignalR is present in v1; polling/refresh is the baseline success criterion.
- `Email` channel is future/extensible in v1: the abstraction, routing, and failure isolation must be implemented and tested, but actual SMTP/Graph delivery may be a stub/no-op that still demonstrates fan-out and failure observability.
- Organizational policy limits are defined as: a set of `NotificationType × Channel` pairs that are mandatory (cannot be disabled). Policy is configured by organization administrators; the default policy mandates `WorkItemOverdue`, `WorkItemBlocked`, `RiskIncreased` on InApp (subject to change by follow-up ADR). This is a reasonable default for compliance.
- Deduplication is enforced via a unique constraint on `(SourceEventId, RecipientId, Channel)` at the persistence layer, plus application-level idempotency check before insert. Concurrency is handled via optimistic handling of duplicate-key violations (treated as already-delivered, not an error).
- Retention: notifications are retained for at least 90 days by default; automatic archival/deletion beyond that is out of scope for v1 but the model includes `CreatedAt` to enable a future retention job without schema change.
- Recipient resolution requires user identity to exist in the identity/organization context (oroidentityserver). Events referencing unknown user IDs result in a dead-letter/skipped entry with observability, not a crash.
- Authorization: notifications are strictly user-scoped. Service-to-service consumption of events is trusted within the Aspire composition; user-facing queries/commands enforce recipient-only access. Hierarchical authorization (manager viewing subordinate notifications) is explicitly out of scope — notifications are private to the recipient.
- Notifications are not themselves audited as business resources beyond `NotificationRead`/`PreferencesUpdated` domain events and standard telemetry; they do not create audit entries in the compliance audit log unless policy says so.
- Integration event payloads contain enough metadata to compose safe titles/links (resource IDs, titles, classification labels) and do NOT contain document bodies or AI payloads — content safety at composition is a defense-in-depth requirement even if upstream payloads are already minimal.
- Dependencies are available: transactional outbox, EventBus with at-least-once delivery and manual ack/retry are provided by BuildingBlocks (per Principles XVII, XXI); notifications reuse that infrastructure.
- Existing repository skills and BuildingBlocks (`Entity`, `AggregateRoot`, `ValueObject`, `Enumeration`, `StronglyTypedId`, `ICommand`/`IQuery`/`ISender`, `Result`/`Error`, `IEndpoint`, `EfRepository`, Outbox) are authoritative and will be reused (Principle I, XXI).

## Dependencies

- SPEC-003 Projects & Work Kanban — provides `WorkItem*` and `ReviewRequested` integration events.
- SPEC-005 Document Management — provides `DocumentUploaded`, `DocumentClassified`, `DocumentApproved` events with classification metadata.
- SPEC-006 LLM Document Intelligence — provides `AiReviewRequested` and related AI operation events (payloads already handled as sensitive).
- SPEC-004 Metrics/Progress/Planning — provides `RiskIncreased` (and similar) event source (Planning/Risk context).
- Principles XVII (Async processing, idempotent handlers), XIX (Security by default), XXI (BuildingBlocks), XXII (Skills).

## Out of Scope

- Real-time push (WebSocket/SignalR/ SSE) for live toast updates — may be a follow-on slice; v1 success is query-based inbox.
- Actual email delivery provider integration (SMTP, SendGrid, Graph) — only abstraction, routing, and failure isolation are required in v1.
- Push notifications to mobile devices.
- User-to-user messaging / collaboration comments (separate collaboration concern).
- Notification grouping/digest (e.g., daily summary email) — each event produces one notification in v1.
- Cross-user notification delegation (manager viewing team notifications) — private inbox only.

## Constitution Traceability

- Principle XVII (Asynchronous Processing): notifications are long-running fan-out via EventBus outbox, at-least-once, idempotent consumers.
- Principle XIX (Security by Default): content safety, tenant-aware recipient resolution, authorization-filtered queries, no secret/payload leakage.
- Principles I/XXI/XXII: reuse `draft/libraries/buildingblocks.md` primitives (AggregateRoot, ValueObject, Enumeration, StronglyTypedId, CQRS, EventBus, EfRepository, Outbox) and respect workspace skills.
- Principles V/VI/VIII/XV/XVI contextual: modular boundary (BC-09 isolated), domain invariants, auditability, organization awareness, API contracts.
