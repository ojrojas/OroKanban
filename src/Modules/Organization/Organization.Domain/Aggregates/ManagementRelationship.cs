using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.ValueObjects;
using Organization.Domain.Events;
using Organization.Domain.Rules;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

public sealed class ManagementRelationship : AggregateRoot<ManagementRelationshipId>
{
    public Guid TenantId { get; private set; }
    public Guid ManagerId { get; private set; }
    public Guid SubordinateId { get; private set; }
    public string Type { get; private set; } = default!;
    public OrganizationUnitId? OrganizationUnitId { get; private set; }
    public DateTime? ValidFrom { get; private set; }
    public DateTime? ValidTo { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ManagementRelationship() { }

    private ManagementRelationship(
        ManagementRelationshipId id,
        Guid tenantId,
        Guid managerId,
        Guid subordinateId,
        string type,
        OrganizationUnitId? unitId,
        DateTime? validFrom,
        DateTime? validTo) : base(id)
    {
        TenantId = tenantId;
        ManagerId = managerId;
        SubordinateId = subordinateId;
        Type = type;
        OrganizationUnitId = unitId;
        ValidFrom = validFrom;
        ValidTo = validTo;
    }

    public static ManagementRelationship Create(
        Guid tenantId,
        Guid managerId,
        Guid subordinateId,
        string type,
        OrganizationUnitId? unitId,
        DateTime? validFrom,
        DateTime? validTo,
        IReadOnlyList<Guid> ancestorsOfSubordinate)
    {
        CheckRule(new ManagerCannotBeSubordinateRule(managerId, subordinateId));
        CheckRule(new SubtreeCannotContainManagerRule(managerId, subordinateId, ancestorsOfSubordinate));

        var entity = new ManagementRelationship(
            ManagementRelationshipId.New(),
            tenantId,
            managerId,
            subordinateId,
            type,
            unitId,
            validFrom,
            validTo);

        entity.RaiseDomainEvent(new ManagerAssignedToUser(
            entity.Id.Value,
            managerId,
            subordinateId,
            type,
            unitId?.Value,
            tenantId));

        return entity;
    }

    public void Revoke()
    {
        ValidTo = DateTime.UtcNow;
        RaiseDomainEvent(new ManagerRemovedFromUser(Id.Value, ManagerId, SubordinateId, TenantId));
    }

    public bool IsActive(DateTime now) =>
        (ValidFrom == null || ValidFrom <= now) && (ValidTo == null || now <= ValidTo);
}
