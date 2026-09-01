using BuildingBlocks.EventBus.Abstractions;

namespace Organization.Contracts.Events;

/// <summary>
/// Published when a management relationship changes (ManagerAssigned / ManagerRemoved / OrganizationUnitMoved).
/// Consumers: Projects, Metrics, Documents, Search, Audit, Notifications — anyone using IManagementHierarchy subtree evaluation.
/// Note: This is the Contracts-only cross-module rule example per T020 — modules communicate via this event, not via direct Infrastructure references.
/// </summary>
public sealed record OrganizationHierarchyChangedIntegrationEvent(
    Guid ActorUserId,
    Guid TargetUserId,
    string ChangeType, // "ManagerAssigned" | "ManagerRemoved" | "UnitMoved"
    Guid? OrganizationUnitId,
    Guid TenantId,
    DateTime ChangedAtUtc
) : IntegrationEvent;
