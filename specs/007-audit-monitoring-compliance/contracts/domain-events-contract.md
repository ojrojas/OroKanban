# Contract: Domain → Integration Events that Produce AuditEntry via Consumer

**Module**: All BCs (producers) → `Audit` (BC-08) consumer (universal, append-only) | **Transport**: `BuildingBlocks.EventBus.RabbitMQ` topic `audit.*` + `integration_events` (durable, publisher confirms, manual ack) | **Delivery**: at-least-once — consumer MUST be idempotent (keyed by `EventId` dedup `audit_consumed_events`)

---

## Producer → Audit mapping (R2 catalog → AuditAction, each 1:1 to at least one DomainEvent)

Same table as `audit-events-contract.md` R2 catalog (31 actions). Each business `DomainEvent` (in `*DbContext.SaveChanges` via `AppDbContextBase` + `IDomainEvent` collection) is staged to `outbox_messages` via `IOutboxWriter.StageAsync(IntegrationEvent)` (JSON-serialized `IntegrationEvent` with `Id=Guid.NewGuid()`, `OccurredOnUtc=DateTime.UtcNow`, `CorrelationId=TenantContext.CorrelationId` + `Activity.Baggage["CorrelationId"]`) in same transaction as business write. `OutboxProcessor` polls `SELECT ... FOR UPDATE SKIP LOCKED` and publishes to RabbitMQ. `AuditEventConsumer` subscribes to `audit.*` wildcard and maps each `IntegrationEvent` type to `AuditAction` via `AuditAction.FromIntegrationEventType(Type)` (1:1).

**Example**: `DocumentApprovedDomainEvent` (in `Documents.Domain` `Document.Approve` → `RaiseDomainEvent(new DocumentApprovedDomainEvent(...))`) → `DocumentApprovedIntegrationEvent(DocumentId, ApproverId, Guid CorrelationId) : IntegrationEvent` (in `Documents.Contracts`) staged in same tx as `Document Status=Approved` → consumed as `AuditEntry` `Action=DocumentApproved`.

---

## IntegrationEvent base (`BuildingBlocks.EventBus.Abstractions.IntegrationEvent`)

```csharp
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public Guid CorrelationId { get; init; } // added for audit R3 (from TenantContext.CorrelationId or Activity.Baggage)
}
```

Concrete subtypes (examples, in their producer `*.Contracts`):

```csharp
// Identity
public sealed record UserLoggedInIntegrationEvent(Guid ActorId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record UserLoginFailedIntegrationEvent(Guid AttemptedIdentity, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
// Documents
public sealed record DocumentUploadedIntegrationEvent(Guid DocumentId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record DocumentApprovedIntegrationEvent(Guid DocumentId, Guid ApproverId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
// Organization
public sealed record HierarchyChangedIntegrationEvent(Guid OrganizationUnitId, Guid? BeforeParentId, Guid? AfterParentId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
// AiProcessing
public sealed record LlmResultApprovedIntegrationEvent(Guid ResultId, Guid ReviewerId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
```

**CorrelationId**: every `*IntegrationEvent` has `Guid CorrelationId` (added to each record as shown). `IOutboxWriter.StageAsync` sets `CorrelationId = TenantContext.CorrelationId ?? Guid.Parse(Activity.Current?.Baggage.FirstOrDefault(b=>b.Key=="CorrelationId").Value ?? Guid.NewGuid().ToString())` at stage time.

---

## Consumer idempotency contract (R3, FR-018)

- **Dedup table**: `audit.audit_consumed_events(EventId PK UNIQUE, ProcessedAt UTC)` — `EventId` is `IntegrationEvent.Id` (stable per `IOutboxWriter.StageAsync` serialization).
- **Handler pseudocode**:

```csharp
public sealed class AuditEventConsumer : IIntegrationEventHandler<IntegrationEvent>
{
    public async Task HandleAsync(IntegrationEvent @event, CancellationToken ct)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var auditContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var masking = scope.ServiceProvider.GetRequiredService<IAuditMaskingPolicy>();
        // Dedup first (same AuditDbContext transaction as entry)
        await using var tx = await auditContext.Database.BeginTransactionAsync(ct);
        var exists = await auditContext.AuditConsumedEvents.AnyAsync(e => e.EventId == @event.Id, ct);
        if (exists) { await tx.RollbackAsync(ct); return; } // duplicate, ACK
        try
        {
            await auditContext.AuditConsumedEvents.AddAsync(new AuditConsumedEvent { EventId = @event.Id, ProcessedAt = DateTime.UtcNow, Action = @event.GetType().Name, CorrelationId = @event.CorrelationId }, ct);
            var maskedSnapshot = masking.Mask(new BeforeAfterSnapshot(@event.BeforeJson, @event.AfterJson)); // ApiKey→***
            var entry = new AuditEntry(AuditEntryId.New(), DateTime.UtcNow, new ActorReference(@event.ActorId, ActorType.User, null), AuditAction.FromIntegrationEventType(@event.GetType()), @event.ResourceType, @event.ResourceId.ToString(), @event.OrganizationId, @event.TenantId, new AuditResult(Result.Success, null), @event.CorrelationId, clientMetadata, maskedSnapshot, previousHash: hashChaining ? ComputeTailHash(@event.TenantId) : null, hash: hashChaining ? ComputeHash(...) : null);
            await auditContext.AuditEntries.AddAsync(entry, ct);
            await auditContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == "23505") // UniqueConstraintViolation on EventId
        {
            await tx.RollbackAsync(ct); // concurrent duplicate race, treat as success
        }
    }
}
```

- **Retry**: `IEventBus` RabbitMQ topic `audit.*` with `IIntegrationEventHandler` manual ack + exponential retries (Polly 3, at-least-once). Duplicate delivery (same `EventId` twice) returns success without second `AuditEntry` (idempotent). `AuditEntry` `AuditId` is *different* `Guid` from `IntegrationEvent.Id` — one `EventId` maps to one `AuditEntry` (not `AuditId==EventId`).
- **Ordering**: not strictly ordered; `AuditEntry.Timestamp` is `DateTime.UtcNow` at consumer time (not `IntegrationEvent.OccurredOnUtc`) — `Timestamp` ordering is consumer processing time, which for `GetOperationTimeline` is still `Timestamp` asc (close to `OccurredOn` for same `CorrelationId` workflow, since consumer processes in `OccurredOn` order via outbox poll `ORDER BY OccurredOn`).

---

## AuditEntry construction details (masked, append-only, correlation)

Same as `audit-events-contract.md` construction snippet: `IAuditMaskingPolicy.Mask` depth-first `JsonDocument` traversal for `Audit:MaskedFields` (`ApiKey,Password,Secret,ConnectionString,Token,CreditCard,PrivateKey` default) replaces values with `"***"` before persistence; `PreviousHash`/`Hash` if chaining else `NULL`; `CorrelationId` from `@event.CorrelationId ?? Activity.Baggage`.

**BeforeAfterSnapshot**: `BeforeJson`/`AfterJson` `jsonb` 1..50k masked — for `DocumentApproved` example `Before: {status:"PendingApproval"} → After: {status:"Approved", apiKey:"***"}`.

**Tamper-evidence**: `PreviousHash = SELECT Hash FROM audit.audit_entries WHERE TenantId=@t ORDER BY Timestamp DESC LIMIT 1 FOR UPDATE` (tail lock). `Hash = SHA256(PreviousHash + "|" + AuditId + "|" + Timestamp.ToString("O") + "|" + Action + "|" + ActorId)` (UTF8, lower hex 64). `VerifyChain()` recomputes. If `ADR-007-01` chooses `NoChaining`, `PreviousHash`/`Hash` stay `NULL` and `DB REVOKE UPDATE, DELETE ON audit.audit_entries FOR app_orokanban` is migration.

