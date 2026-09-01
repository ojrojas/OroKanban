# Contract: Notifications Inbox (GetMyNotifications, GetUnreadCount)

**Module**: `Notifications` (BC-09) | **Base path**: `/api/notifications` | **Auth**: Bearer JWT (`sub`=`RecipientId`, `tenant_id` via `TenantContext`) | **Conventions**: `Result→HTTP` 400/403/404/409, recipient-only `Specification<Notification>` pre-filter `RecipientId==callerId` BEFORE fetch, pagination envelope `{items, totalCount, page, pageSize, link}`, safe `Title`/`Body`/`Link` only.

---

## GET /api/notifications — GetMyNotifications

**Query**: `GetMyNotificationsQuery : IQuery<Result<Paged<NotificationResponse>>>`

Returns current user's notifications paginated newest-first, optionally filtered by read state and type. Never returns other users' rows.

```json
// Request query string (all filters optional except caller from JWT):
// GET /api/notifications?page=1&pageSize=20&unreadOnly=false&type=WorkItemAssigned
// Headers: Authorization: Bearer <jwt with sub=callerId, tenant_id=tid>
//
// Response 200 — ordered CreatedAt desc, paginated, recipient-filtered
{
  "items": [
    {
      "notificationId": "guid (NotificationId)",
      "recipientId": "guid (callerId)",
      "type": "WorkItemAssigned",            // NotificationType display name
      "typeId": 1,
      "channel": "InApp",                    // Channel display name
      "channelId": 1,
      "title": "You were assigned work item \"Sprint-12\"",
      "body": "Assigned by Alice — open to view",
      "link": "/projects/guid/work-items/guid",
      "deliveryState": "Delivered",
      "deliveryStateId": 2,
      "createdAt": "2026-09-01T12:00:01Z",
      "readAt": null,                        // null when unread
      "sourceEventId": "guid",
      "sourceResourceId": "guid | null",
      "sourceResourceType": "WorkItem | Document | LlmResult | Project | null",
      "correlationId": "guid | null"
    }
  ],
  "totalCount": 42,
  "page": 1,
  "pageSize": 20,
  "link": "<http://localhost:5000/api/notifications?page=2&pageSize=20>; rel=\"next\""
}
// Ordering: DESC by CreatedAt (newest first); secondary by NotificationId for stable cursor
// Filtering: ?unreadOnly=true → WHERE ReadAt IS NULL; ?type=WorkItemAssigned → WHERE NotificationTypeId==1 (enum parse case-insensitive)
// Authorization: handler does `var spec = new NotificationByRecipientSpec(callerId).And(new IsUnreadSpec(unreadOnly)).And(new TypeFilterSpec(type?)).And(...).ApplyOrderByDescending(n=>n.CreatedAt).ApplyPaging((page-1)*pageSize, pageSize).AsNoTracking`
//   WHERE RecipientId==callerId is the FIRST predicate — injected by handler, never by client query string (client cannot query other recipientId)
//   If caller queries with no notifications → 200 empty items totalCount=0 (not 404)
//   Cross-user attack: /api/notifications?recipientId=otherGuid → ignored, still filters by callerId (generic deny, no leak)
// Tenant isolation: WHERE TenantId==ctx.TenantId if TenantId column populated; mismatch → 0 rows
// Performance: INDEX(RecipientId, CreatedAt desc) + DeliveryState filter ensures <500ms p95 for 10k rows paginated
// Errors: 400 Validation (page/pageSize 1..100, type unknown → Error.Validation("NotificationType.Unknown")), 401 unauthenticated, 403 never (empty list instead to avoid enumeration), 409 never
```

**Handler** (`GetMyNotificationsHandler : IQueryHandler<GetMyNotificationsQuery, Result<Paged<NotificationResponse>>>`):

1. Resolve `callerId = TenantContext.UserId` (from JWT `sub`), `tenantId = TenantContext.TenantId`.
2. Validate pagination (`page` 1..100, `pageSize` 1..100 default 20 cap 100) via `GetMyNotificationsValidator`.
3. Build `Specification<Notification>` with mandatory `RecipientId==callerId` predicate + optional `Type`/`UnreadOnly` + `TenantId==tenantId` if present + `ApplyOrderByDescending(CreatedAt)` + paging.
4. `IRepository<Notification>.FindAsync(spec)` + `CountAsync` for total.
5. Map `Notification` → `NotificationResponse` DTO (safe fields only).

---

## GET /api/notifications/unread-count — GetUnreadCount

**Query**: `GetUnreadCountQuery : IQuery<Result<UnreadCountResponse>>`

Returns count of unread notifications for the current caller. Efficient without full scan.

```json
// Request: GET /api/notifications/unread-count
// Headers: Authorization: Bearer <jwt>
//
// Response 200
{
  "recipientId": "guid",
  "unreadCount": 5
}
// Filtering: WHERE RecipientId==callerId AND ReadAt IS NULL AND TenantId==callerTenant
// Performance: INDEX(RecipientId) WHERE ReadAt IS NULL (partial index) ensures <500ms p95 for 10k unread
// Caching: no cache in MVP — count is computed on read; optional Redis cached via ICache later
// Errors: 401 unauthenticated; never 403/404
```

**Handler** (`GetUnreadCountHandler`):

1. Resolve `callerId`.
2. `var spec = new UnreadNotificationsSpec(callerId, tenantId) { ApplyAsNoTracking = true }; var count = await repository.CountAsync(spec, ct);`
3. Return `new UnreadCountResponse(callerId, count)`.

**Cross-cutting DTO** (shared with `MarkRead`):

```json
{
  "notificationId": "guid",
  "recipientId": "guid",
  "type": "string 1..100 (NotificationType name)",
  "typeId": "int",
  "channel": "InApp | Email",
  "channelId": "int",
  "title": "string 1..200 (safe)",
  "body": "string 1..2000 (safe, metadata+link text)",
  "link": "string 1..500 (deep link)",
  "deliveryState": "Pending|Delivered|Failed",
  "deliveryStateId": "int",
  "createdAt": "DateTime UTC ISO-8601",
  "readAt": "DateTime UTC ISO-8601 | null",
  "sourceEventId": "guid",
  "sourceResourceId": "guid | null",
  "sourceResourceType": "string | null",
  "correlationId": "guid | null"
}
```

**Paginated envelope**:

```json
{
  "items": [ { "...NotificationResponse..." } ],
  "totalCount": "int",
  "page": "int",
  "pageSize": "int",
  "link": "string | null (Link header also set as HTTP header rel=next)"
}
```

**Tenant isolation**: `WHERE TenantId==ctx.TenantId` is first predicate after `RecipientId==callerId` in both handlers; tenant mismatch → empty result (not 403) to avoid membership enumeration (same as documents cross-tenant 404 shadow).

**Pagination**: `page` 1..100, `pageSize` 1..100 default 20 cap 100, `Link` header `rel="next"` when `skip+take < totalCount`.

**Errors envelope** (`Result→HTTP` via `GlobalExceptionHandler`):

```json
// 400 Validation
{ "title": "Validation failed", "status": 400, "errors": { "Type": ["Unknown NotificationType 'Foo'"] } }
// 401 — unauthenticated (JWT missing/expiry)
// 404 — tenant-aware shadow only for MarkRead individual resource (see commands contract), not for list/count
```

