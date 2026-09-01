# Contract: Domain → Integration Events

**Module**: `Projects` (BC-03) | **Pattern**: Domain events dispatched inside `AppDbContextBase.SaveChanges` → staged via `IOutboxWriter` → `OutboxProcessor` (RabbitMQ topic exchange, publisher confirms, manual ack, exponential retries) → integration events consumed by `Audit`/`Notifications`/`Search`/`Metrics` BCs. Per `draft/libraries/buildingblocks.md` EventBus conventions and constitution XVII (handlers idempotent, at-least-once).

## Domain events (Projects.Domain — in-process, raised by aggregates)

| Aggregate | Domain event | Raised when | Payload |
|-----------|--------------|-------------|---------|
| `Project` | `ProjectCreated` | `Project.Create` succeeds | `{ ProjectId, TenantId, OwnerId, ManagerId, Name, Status, Priority, Criticality }` |
| `Project` | `ProjectMemberAdded` | `AddMember` | `{ ProjectId, UserId, Role, TenantId }` |
| `Project` | `ProjectMemberRemoved` | `RemoveMember` | `{ ProjectId, UserId, TenantId }` |
| `Project` | `ProjectStatusChanged` | `ChangeStatus` | `{ ProjectId, From, To, Actor, TenantId }` |
| `Project` | `MilestoneReached` | milestone `IsReached` flips true | `{ ProjectId, MilestoneId, Title, ReachedAt }` |
| `WorkItem` | `WorkItemCreated` | `Create` with `Version=1` | `{ WorkItemId, ProjectId, TenantId, Type, Status=Backlog, Priority, Criticality, ParentId }` |
| `WorkItem` | `WorkItemStatusChanged` | `ChangeStatus` passes `TransitionIsAllowedRule` + evaluator | `{ WorkItemId, ProjectId, From, To, Actor, Version, TenantId }` |
| `WorkItem` | `WorkItemAssigned` | first assign | `{ WorkItemId, ProjectId, AssigneeId, AssignerId, TenantId, Version }` |
| `WorkItem` | `WorkItemReassigned` | subsequent assign | `{ WorkItemId, OldAssigneeId, NewAssigneeId, TenantId }` |
| `WorkItem` | `WorkItemReparented` | `ReparentWorkItem` | `{ WorkItemId, OldParentId, NewParentId, ProjectId, TenantId, Version }` |
| `WorkItem` | `WorkItemCompleted` | `CompleteWorkItem` / `InReview→Completed` | `{ WorkItemId, ProjectId, CompletedAt, ActualEffort, TenantId }` |
| `WorkItem` | `ProgressRecalculated` | subtask progress triggers ancestor recalc | `{ WorkItemId, OldProgress, NewProgress, Inputs }` |
| `WorkItem` | `WorkItemBlocked` | derivation reports blocked (informational) | `{ WorkItemId, BlockedByIds, TenantId }` |
| `WorkItem` | `DependencyAdded` | `AddDependency` (no cycle) | `{ DependencyId, DependentId, PrincipalId, Type, ProjectId, TenantId }` |
| `WorkItemDependency` | `DependencyRemoved` | `RemoveDependency` | `{ DependencyId, DependentId, PrincipalId, TenantId }` |

## Integration events (Projects.Contracts — cross-BC, via outbox)

Published as `IntegrationEvent` subclasses in `Projects.Contracts/Events/` (consumed by `Audit`, `Notifications`, `Search`, `Metrics`). Exchange `integration_events`, topic routing `project.*` / `workitem.*` / `dependency.*`.

```csharp
// Example — emitted by OutboxProcessor
public sealed record WorkItemStatusChangedIntegrationEvent(
    Guid WorkItemId, Guid ProjectId, Guid TenantId,
    string FromStatus, string ToStatus, Guid ActorId, int Version, DateTime ChangedAt
) : IntegrationEvent;

public sealed record WorkItemAssignedIntegrationEvent(
    Guid WorkItemId, Guid ProjectId, Guid TenantId,
    Guid AssigneeId, Guid AssignerId, int Version
) : IntegrationEvent;

public sealed record ProjectCreatedIntegrationEvent(Guid ProjectId, Guid TenantId, string Name) : IntegrationEvent;
public sealed record DependencyAddedIntegrationEvent(Guid DependencyId, Guid DependentId, Guid PrincipalId, string Type, Guid TenantId) : IntegrationEvent;
```

## Outbox + audit wiring

- Handlers call `await outbox.StageAsync(new <IntegrationEvent>(...), ct)` then `await unitOfWork.SaveChangesAsync(ct)` — domain events + outbox staged atomically (same transaction).
- `OutboxProcessor` (`BackgroundService`, manual ack, exponential retries) publishes to RabbitMQ topic `integration_events` with publisher confirms.
- `Audit` BC (future) subscribes to all `project.*`/`workitem.*`/`dependency.*` topics and appends audit entries (append-only, never mutate). Deny audits (`authorization.denied`) are staged the same way — the evaluator's deny path stages via `IOutboxWriter` inside the same transaction as the denied command attempt.
- Notification BC (SPEC-008) subscribes to `WorkItemAssignedIntegrationEvent` / `WorkItemStatusChangedIntegrationEvent` for notifications.
- Search/Metrics subscribe to `WorkItem*IntegrationEvent` for indexing/progress wiring.
- Handlers are **idempotent** (at-least-once delivery): `Audit` deduplicates on `IntegrationEventId`, `Projects` handlers re-check `Version` / existence before applying.

## Consumption by Organization BC

`Organization` consumes `ProjectCreated`/`ProjectMemberAdded`→updates its `IProjectMembership` view (if it keeps a read cache), and `WorkItemAssigned` for manager-auditable assignment history. Publishing is always from `Projects` — `Organization` never writes project membership directly.

## Naming

Domain events: `PascalCase` `...DomainEvent` in `Projects.Domain/Events/`; integration events: `...IntegrationEvent` in `Projects.Contracts/Events/` per BuildingBlocks `IntegrationEvent` base.
