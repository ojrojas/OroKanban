# Contract: Notification Preferences (GetPreferences, UpdatePreferences)

**Module**: `Notifications` (BC-09) | **Base path**: `/api/notifications/preferences` | **Auth**: Bearer JWT (`sub`=`UserId`, `tenant_id`) | **Conventions**: `Result→HTTP` 400/403/404/409, per-user `NotificationPreference` aggregate with optimistic `RowVersion`, validation via `Validator<T>`, merge with org policy at dispatcher time.

---

## GET /api/notifications/preferences — GetPreferences

Returns current user's raw preferences + effective (policy-merged) for UI to render mandated toggles as disabled.

```json
// Request: GET /api/notifications/preferences
// Headers: Authorization: Bearer <jwt with sub=userId>

// Response 200 — raw user matrix + effective (merged) + mandated set
{
  "userId": "guid (callerId)",
  "tenantId": "guid",
  "rawPreferences": {
    "WorkItemAssigned": { "InApp": true, "Email": false },
    "DocumentApproved": { "InApp": false, "Email": true },
    "RiskIncreased": { "InApp": false, "Email": false }  // user tried to disable mandated
  },
  "effectivePreferences": {
    "WorkItemAssigned": { "InApp": true, "Email": false },
    "DocumentApproved": { "InApp": false, "Email": true },
    "RiskIncreased": { "InApp": true, "Email": false }   // policy overrides → true despite raw false
  },
  "mandatedTypes": [
    { "type": "WorkItemOverdue", "typeId": 3, "channel": "InApp", "channelId": 1 },
    { "type": "WorkItemBlocked", "typeId": 4, "channel": "InApp", "channelId": 1 },
    { "type": "RiskIncreased", "typeId": 11, "channel": "InApp", "channelId": 1 }
  ],
  "defaultForNewUser": {
    "InApp": true,  // generic default for any type not in raw
    "Email": false
  },
  "updatedAt": "2026-09-01T12:00:00Z | null when never set",
  "rowVersion": "base64 string | null (concurrency token for PUT)"
}
// Ordering: keys alphabetical by NotificationType name for stable JSON (client sorts)
// Missing user row → rawPreferences = {} (empty), effective = defaults + mandated, updatedAt = null, rowVersion = null
// Authorization: handler filters by UserId==callerId only — no query param for other user (ignored)
// Tenant: WHERE TenantId==ctx.TenantId
// Errors: 401 unauthenticated
```

**Handler** (`GetPreferencesHandler : IQueryHandler<GetPreferencesQuery, Result<PreferencesResponse>>`):

1. Resolve `callerId`, `tenantId`.
2. `var prefs = await repository.FirstOrDefaultAsync(new PreferenceByUserSpec(callerId), ct);` — if null → empty raw.
3. Load `mandated = INotificationPolicy.MandatedTypes`.
4. Build `effective` by applying `IsEnabled(type,channel,callerId, raw)` for each `NotificationType` value (11 seeded values) and each `Channel` (InApp, Email).
5. Return `PreferencesResponse` (never 404).

---

## PUT /api/notifications/preferences — UpdatePreferences

Replaces the current user's preference matrix (full replace, not patch). Validates enumerations, enforces optimistic concurrency, emits `PreferencesUpdated` domain event. Policy-mandated types cannot be suppressed at effective evaluation time — but raw false is still persisted (UI shows overridden).

```json
// Request: PUT /api/notifications/preferences
// Headers: Authorization: Bearer <jwt>, Content-Type: application/json
// Optional: If-Match: "<rowVersion base64>" OR field rowVersion in body — concurrency guard
{
  "preferences": {
    "WorkItemAssigned": { "InApp": true, "Email": false },
    "WorkItemOverdue": { "InApp": false, "Email": false }, // will be ineffective — mandated InApp overrides
    "DocumentUploaded": { "InApp": false, "Email": true },
    "DocumentApproved": { "InApp": true, "Email": false }
  },
  "rowVersion": "base64 string | null (base64(RowVersion) from GET; null on first creation)"
}

// Response 200 — returns updated effective view
{
  "userId": "guid",
  "tenantId": "guid",
  "rawPreferences": { "...same as request..." },
  "effectivePreferences": { "...mandated overrides applied..." },
  "updatedAt": "2026-09-01T12:00:05Z",
  "rowVersion": "new base64"
}

// Examples:
// 400 Validation — unknown type or channel
{
  "title": "Validation failed", "status": 400,
  "errors": { "preferences": ["Unknown NotificationType 'FooType'"], "preferences.WorkItemAssigned.InApp": ["must be boolean"] }
}
// 409 Conflict — concurrent modification
{
  "title": "Concurrency conflict", "status": 409,
  "detail": "Preferences were modified by another request. Fetch latest and retry."
}
// 401 unauthenticated
```

**Command**: `UpdatePreferencesCommand(UserId callerId, Dictionary<int,Dictionary<int,bool>> Preferences, byte[]? ExpectedRowVersion, Guid TenantId) : ICommand<Result<PreferencesResponse>>`

**Handler** (`UpdatePreferencesHandler`):

1. Validate via `UpdatePreferencesValidator`: `Preferences` keys must be parseable as known `NotificationType` ids (1..100), inner keys as known `Channel` ids (1..10), values bool; unknown type/channel → `Error.Validation("NotificationType.Unknown")` → 400; no partial update — entire `Preferences` is replaced (unknown keys rejected, not ignored).
2. Load existing `NotificationPreference` via `PreferenceByUserSpec(callerId)` `FOR UPDATE` (tracked). If not found → `NotificationPreference.Create(callerId, tenantId, preferences)`. Else check `RowVersion` equality if `ExpectedRowVersion` supplied → if mismatch → `Error.Conflict("Preferences.Concurrency")` → 409.
3. Domain call `preference.Update(preferences)` (validates invariants, sets `UpdatedAt=UtcNow`, updates `PreferencesJson`, bumps `RowVersion`).
4. `await repository.UpdateAsync?` — but `EfRepository` uses tracked entity so `SaveChangesAsync` persists; however `NotificationPreference` is AggregateRoot, so handler does `await unitOfWork.SaveChangesAsync(ct)` inside same `NotificationsDbContext` transaction as domain event staging.
5. `preference.RaiseDomainEvent(new PreferencesUpdated(...))` → dispatched via `AppDbContextBase` domain dispatcher → outbox if needed for audit (audit consumer picks up `PreferencesUpdated` integration if mapped).
6. Return mapped `PreferencesResponse` with `effective` merged via `INotificationPolicy`.

**Concurrency**: `RowVersion` (`byte[]` `IsRowVersion()`). Client sends `rowVersion` from `GET` in `If-Match` header or body field. Handler uses EF `ConcurrencyCheck` — mismatch → 409 with message to refetch. Missing `rowVersion` on first creation (no row) → insert succeeds.

**Policy merge visibility**: `effectivePreferences` in response shows the dispatcher result — UI uses `mandatedTypes` to disable toggles (e.g., `WorkItemOverdue InApp` toggle rendered disabled + tooltip `Required by organization policy`). Raw persistence still allows `false` for mandated so audit of user intent is preserved.

**Bulk vs per-type**: This slice replaces full matrix. Per-type `PATCH /preferences/{type}` is out of scope (not needed for 11 types × 2 channels, single `PUT` is atomic and validates all).

**Tenant**: `WHERE UserId==callerId AND TenantId==callerTenant`. Cross-tenant `PUT` with forged `tenant_id` in JWT → tenant mismatch at handler → row not found would create row for callerTenant, not overwrite victim tenant's row (tenant isolation).

**DTOs**:

```csharp
record PreferencesResponse(Guid UserId, Guid TenantId,
    IReadOnlyDictionary<string,IReadOnlyDictionary<string,bool>> RawPreferences,
    IReadOnlyDictionary<string,IReadOnlyDictionary<string,bool>> EffectivePreferences,
    IReadOnlyList<MandatedTypeDto> MandatedTypes,
    DefaultDto DefaultForNewUser, DateTime? UpdatedAt, string? RowVersion);
record MandatedTypeDto(string Type, int TypeId, string Channel, int ChannelId);
record UpdatePreferencesRequest(Dictionary<string,Dictionary<string,bool>> Preferences, string? RowVersion);
```

String keys in JSON are enum names (`WorkItemAssigned`) for readability; server accepts both name and numeric id (name preferred, numeric tolerated via custom `JsonConverter` for `NotificationType`).

