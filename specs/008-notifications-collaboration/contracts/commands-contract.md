# Contract: MarkRead Command

**Module**: `Notifications` (BC-09) | **Base path**: `/api/notifications/{id}` | **Auth**: Bearer JWT (`sub`=`UserId`) | **Conventions**: `Result→HTTP` 400/403/404/409, recipient-only idempotent, domain event `NotificationRead`.

---

## POST /api/notifications/{notificationId}/read — MarkRead

Transitions a single notification from unread to read for its owner. Idempotent if already read. Cross-user attempt is rejected (no leak).

```json
// Request: POST /api/notifications/{notificationId}/read
// Headers: Authorization: Bearer <jwt with sub=callerId>
// Body: {} (empty) — idempotency does not need ETag
//
// Response 200 — success (idempotent)
{
  "notificationId": "guid",
  "recipientId": "guid",
  "readAt": "2026-09-01T12:00:05Z",
  "wasAlreadyRead": false   // true when idempotent second call
}

// Second call (idempotent) — same notificationId
// Response 200 — still success, same readAt, no duplicate domain event
{
  "notificationId": "guid",
  "recipientId": "guid",
  "readAt": "2026-09-01T12:00:05Z",
  "wasAlreadyRead": true
}

// Errors:
// 400 — notificationId not a valid Guid
{ "title": "Validation failed", "status": 400, "errors": { "notificationId": ["Invalid Guid"] } }

// 401 — unauthenticated

// 403 — caller is authenticated but not the recipient (generic deny, no enumeration of existence)
// { "title": "Forbidden", "status": 403 }

// 404 — notification not found (also when found but in other tenant — 404 shadow to avoid enumeration)
// { "title": "Not found", "status": 404 }

// Concurrency note: concurrent POST /read for same id → one sets ReadAt first, other sees ReadAt!=null and returns wasAlreadyRead=true without second domain event
```

**Command**: `MarkReadCommand(Guid NotificationId, Guid CallerId, Guid TenantId) : ICommand<Result<MarkReadResponse>>`

**Validator** (`MarkReadValidator`): `NotificationId != Guid.Empty`.

**Handler** (`MarkReadHandler : ICommandHandler<MarkReadCommand, Result<MarkReadResponse>>`):

1. Resolve `callerId = TenantContext.UserId`, `tenantId = TenantContext.TenantId`, `notificationId` from route.
2. Load `Notification` via `IRepository<Notification>.FirstOrDefaultAsync(new NotificationByIdSpec(notificationId), ct)` (tracked). If not found → `Error.NotFound("Notification.NotFound")` → 404 (tenant-aware shadow: if found but `TenantId != callerTenant` → also 404).
3. Authorization: if `notification.RecipientId != callerId` → `Error.Forbidden("Notification.NotOwner")` → 403 generic (`403` not `404` here? Chosen: cross-user → 403 to signal ownership violation; cross-tenant → 404 shadow per XV to avoid enumeration that recipient exists. If both recruited, spec: `MarkRead` where Dave is not owner of Eve's notification → rejected. So check `RecipientId != callerId` → 403 regardless of tenant match (explicit ownership violation). If `RecipientId == callerId` but `TenantId` mismatch → 404.
4. Idempotency: if `notification.ReadAt != null` → return `new MarkReadResponse(notification.Id.Value, notification.RecipientId, notification.ReadAt.Value, wasAlreadyRead: true)` WITHOUT raising `NotificationRead` domain event (no duplicate) and WITHOUT `SaveChanges`.
5. Else domain call `notification.MarkRead()`  → sets `ReadAt = DateTime.UtcNow` and `RaiseDomainEvent(new NotificationRead(notification.Id, notification.RecipientId, notification.ReadAt.Value))`.
6. `await unitOfWork.SaveChangesAsync(ct)` — persists `ReadAt` and outbox `NotificationRead` for audit (007 audit consumer can pick up).
7. Return `MarkReadResponse` with `wasAlreadyRead=false`.

**Domain** (`Notification.MarkRead()`):

```csharp
public void MarkRead()
{
    if (ReadAt != null) return; // idempotent inside aggregate too
    ReadAt = DateTime.UtcNow;
    RaiseDomainEvent(new NotificationRead(Id, RecipientId, ReadAt.Value));
}
```

**Specification**: `NotificationByIdSpec` (`Where(n => n.Id == id).AsTracking()` for handler, `AsNoTracking` for read). No extra tenant filter beyond handler's manual check (handler already checks tenant after load to produce correct 403/404 mapping).

**Bulk operation**: `POST /api/notifications/read` with body `{"notificationIds": [guid,...]}` batch is out of scope for MVP (individual mark only). Future batch can be added without breaking individual endpoint.

**Ordering**: `MarkRead` does not affect `CreatedAt` ordering in inbox; `ReadAt` is independent.

**Auditing**: `NotificationRead` is forwarded via outbox to `AuditEventConsumer` if audit catalog includes reads (per Principle VIII). The handler does not directly write to `Audit`; audit is terminal consumer.
