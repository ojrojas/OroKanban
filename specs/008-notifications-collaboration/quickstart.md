# Quickstart: Notifications and Collaboration (BC-09)

**Feature**: 008-notifications-collaboration | **Prereqs**: `.NET 10 SDK 10.0.400`, `Podman`, `pnpm`, Aspire workload (`dotnet workload update` + `aspire` CLI), Postgres/RabbitMQ/Redis via Aspire (no extra infra for notifications).

Validation scenarios prove the feature works end-to-end. Each can be run as a test or manual `curl` flow. Implementation details (models/services) belong in `tasks.md` / code, not here.

---

## 1. Prereqs & Setup

```bash
# Clone + restore
git clone <orokanban> && cd OroKanban
dotnet build OroKanban.slnx

# Start Aspire composition (postgres/rabbitmq/redis + api + oroidentityserver external)
aspire run --project OroKanban.AppHost

# Api base: https://localhost:<apphost-api-port>  (discover via Aspire dashboard)
# Get JWT for two users (assignee + other) — via oroidentityserver token endpoint or seed admin flow
# Example: use seeded tenant admin + create users via POST /api/organization/users (from 002)
# Capture: TENANT_ID, ALICE_TOKEN (recipient), BOB_TOKEN (other user), DAVE_TOKEN
export API=https://localhost:5001
export ALICE_TOKEN=$(curl -s -X POST "$IDENTITY/authorities/connect/token" ... | jq -r .access_token)
export TENANT_ID=<from JWT tenant_id claim>
```

**Alternative test harness**: Run integration tests directly without manual tokens:

```bash
dotnet test tests/Notifications.Tests --filter "DispatcherIdempotency"
dotnet test src/Modules/Notifications --filter "ContentSafety"
```

---

## 2. Scenario A — Single assignment produces one InApp notification

*Proves User Story 1 + FR-001/002/004 + SC-001.*

```bash
# Publish WorkItemAssigned via integration event (triggered by Projects BC)
# In test: directly publish via IEventBus or call Projects API that stages the event via outbox
curl -s -X POST "$API/api/projects/PROJ_ID/work-items" \
  -H "Authorization: Bearer $ALICE_TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Sprint-12","assigneeId":"<ALICE_USER_ID>","typeId":1}'

# Or in integration test: await eventBus.PublishAsync(new WorkItemAssignedIntegrationEvent(workItemId, projId, tenantId, aliceId, assignerId))

# Assert: Alice sees one notification within 5s
curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $ALICE_TOKEN" | jq
# Expected: items[0].type == "WorkItemAssigned" && items[0].recipientId == ALICE_USER_ID && items[0].channel=="InApp" && items[0].link == "/projects/PROJ_ID/work-items/WORK_ID"

curl -s "$API/api/notifications/unread-count" -H "Authorization: Bearer $ALICE_TOKEN" | jq
# Expected: {"unreadCount":1}
```

**Expected**: 1 `InApp` row for Alice, safe title `You were assigned work item "..."` + link, zero rows for Bob.

---

## 3. Scenario B — Duplicate redelivery is idempotent

*Proves User Story 2 + FR-006 + SC-002.*

```bash
# Redeliver same event (same Id) 10 times — via RabbitMQ redelivery / test loop
for i in {1..10}; do curl -s -X POST "$API/test/publish-work-assigned" -d '{"eventId":"evt-123","workItemId":"WID","assigneeId":"ALICE"}' -H "Authorization: Bearer $ALICE_TOKEN"; done

curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $ALICE_TOKEN" | jq '.totalCount'
# Expected: still 1 — dedupe key (SourceEventId,RecipientId,Channel) unique violation swallowed
```

**Test gate**: `ConsumerIdempotencyTests.DuplicateEvent_Produces_OneNotification` — publish same `IntegrationEvent(Id=evt-123)` twice → `count == 1`, no second `NotificationCreated`.

---

## 4. Scenario C — Preferences within org policy

*Proves User Story 3 + FR-010/011 + SC-005.*

```bash
# Carol disables DocumentUploaded InApp
curl -s -X PUT "$API/api/notifications/preferences" \
  -H "Authorization: Bearer $CAROL_TOKEN" -H "Content-Type: application/json" \
  -d '{"preferences":{"DocumentUploaded":{"InApp":false,"Email":false}}}'

# Publish DocumentUploaded targeting Carol
curl -s -X POST "$API/test/publish-document-uploaded" -d '{"documentId":"D1","ownerId":"CAROL"}'

curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $CAROL_TOKEN" | jq '.items | map(select(.type=="DocumentUploaded")) | length'
# Expected: 0 — skipped by preference (no row, log observed)

# Publish RiskIncreased (mandated InApp) targeting Carol despite opt-out
curl -s -X PUT "$API/api/notifications/preferences" \
  -H "Authorization: Bearer $CAROL_TOKEN" -H "Content-Type: application/json" \
  -d '{"preferences":{"RiskIncreased":{"InApp":false}}}'

curl -s -X POST "$API/test/publish-risk-increased" -d '{"projectId":"PROJ_ID","tenantId":"TID","score":9}'

curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $CAROL_TOKEN" | jq '.items | map(select(.type=="RiskIncreased")) | length'
# Expected: 1 — policy overrides preference (IsEnabled returns true for mandated)
```

**Test gate**: `PolicyMergeTests.MandatedType_OverridesPreferenceFalse` + `NonMandated_Disabled_Skips`.

---

## 5. Scenario D — MarkRead + UnreadCount

*Proves User Story 4 + FR-012/013/014.*

```bash
# Seed: Alice has 1 unread from Scenario A
curl -s "$API/api/notifications/unread-count" -H "Authorization: Bearer $ALICE_TOKEN"
# -> {"unreadCount":1}

NOTIF_ID=$(curl -s "$API/api/notifications?page=1&pageSize=1" -H "Authorization: Bearer $ALICE_TOKEN" | jq -r '.items[0].notificationId')

curl -s -X POST "$API/api/notifications/$NOTIF_ID/read" -H "Authorization: Bearer $ALICE_TOKEN" | jq
# -> {"readAt":"2026-09-01T...","wasAlreadyRead":false}

curl -s "$API/api/notifications/unread-count" -H "Authorization: Bearer $ALICE_TOKEN" | jq
# -> {"unreadCount":0}

# Idempotent second read
curl -s -X POST "$API/api/notifications/$NOTIF_ID/read" -H "Authorization: Bearer $ALICE_TOKEN" | jq
# -> {"wasAlreadyRead":true} — no duplicate NotificationRead domain event

# Cross-user forbidden — Bob tries to mark Alice's notification
curl -s -X POST "$API/api/notifications/$NOTIF_ID/read" -H "Authorization: Bearer $BOB_TOKEN" -w "%{http_code}\n"
# -> 403 Forbidden (generic, no leak)

# Pagination — Alice with 50 notifications
curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $ALICE_TOKEN" | jq '.items | length'
# -> 20 ordered CreatedAt desc, Link header rel=next present
```

---

## 6. Scenario E — Channel fan-out, failure isolation, extensibility

*Proves User Story 5 + FR-007/008/009 + SC-003/SC-007.*

```bash
# Configure Email channel to throw (test flag EmailChannel:ShouldFail=true) then publish
curl -s -X POST "$API/test/configure-email-fail" -d '{"shouldFail": true}'

curl -s -X POST "$API/test/publish-work-completed" -d '{"workItemId":"W2","assigneeId":"ALICE"}'

curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $ALICE_TOKEN" | jq '.items | map(select(.type=="WorkItemCompleted")) | length'
# Expected: 1 InApp still delivered
# Logs: Email channel failure visible via structured log {"channel":"Email","deliveryState":"Failed","error":"..."} and DeliveryState=Failed row for Email channel (separate row)
# Retry does not duplicate InApp (dedupe per-channel)

# Extensibility check (integration test adds stub channel)
dotnet test --filter "ChannelFanOutTests.NewChannel_Receives_WithoutDispatcherChange"
# Expected: pass — adding TestChannel via DI `IChannel` does not change InApp count
```

---

## 7. Scenario F — Content safety (metadata+links only)

*Proves User Story 6 + FR-005 + SC-004.*

```bash
# Trigger Confidential document approval
curl -s -X POST "$API/test/publish-document-approved" -d '{"documentId":"CONF-1","classification":"Confidential","approverId":"ADMIN"}'

curl -s "$API/api/notifications?page=1&pageSize=20" -H "Authorization: Bearer $ALICE_TOKEN" | jq '.items[] | select(.type=="DocumentApproved") | {title, body, link}'
# Expected: title contains documentId/classification label, body contains link text, body DOES NOT contain 50KB document bytes or content
# Assert: body length < 500, body not containing "SENSITIVE_CONTENT_MARKER" injected as document body
# AI payload leak check similarly:
curl -s -X POST "$API/test/publish-ai-result" -d '{"resultId":"R1","documentId":"D1","provenanceJson":"{\"model\":\"gpt-4o\"}","payload":"LARGE_AI_PAYLOAD_50KB"}'
curl -s "$API/api/notifications" -H "Authorization: Bearer $ALICE_TOKEN" | jq '.items[] | select(.type=="AiReviewRequested") | .body'
# Expected: body contains ResultId + link, not payload
```

**Unit gate** (must pass before integration): `dotnet test --filter ContentSafetyTests` — each `NotificationType` template asserted to not contain `DocumentBody`/`AiPayload`.

---

## 8. Runbook — full validation in one command

```bash
# Unit
dotnet test tests/Notifications.Tests --filter "PolicyMerge|DedupeKey|ContentSafety"

# Integration (requires Aspire + Testcontainers Postgres/RabbitMQ)
dotnet test tests/Notifications.Tests.Integration --filter "DispatcherIdempotency|ChannelFanOut|Preference|MarkRead|CrossUserIsolation"

# E2E (publishes real events through outbox → RabbitMQ → dispatcher → query)
dotnet test tests/Notifications.Tests.E2E --filter "WorkItemAssignedFlow"
```

**Pass criteria**: All scenarios above green, SC-002 0% duplicate under 10× redelivery, SC-004 0 bytes leakage across all 11 types, SC-005 preference/policy merge 100%, SC-006 p95 <500ms for 10k inbox, SC-003 100% InApp when Email fails.

---

## 9. Troubleshooting

- Notifications not appearing: check `OutboxProcessor` logs for `Published NotificationCreated` and RabbitMQ queue depth via Aspire dashboard; unread count requires `RecipientId==callerId` JWT `sub`.
- 403 vs 404 on MarkRead: `RecipientId != callerId` → 403; `TenantId` mismatch or missing row → 404 shadow.
- Preference 409: refetch `GET /preferences` to get fresh `rowVersion` and retry `PUT`.
- Duplicate notifications: check `UNIQUE(SourceEventId,RecipientId,Channel)` index exists via `\d notifications.notifications` in psql.

