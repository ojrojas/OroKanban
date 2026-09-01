using BuildingBlocks.Kernel.Domain.Entities;
using Organization.Domain.Events;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

public sealed class OrganizationUnit : AggregateRoot<OrganizationUnitId>
{
    public Guid TenantId { get; private set; }
    public OrganizationUnitId? ParentId { get; private set; }
    public string Name { get; private set; } = default!;
    public HierarchyPath HierarchyPath { get; private set; } = default!;
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private OrganizationUnit() { }

    private OrganizationUnit(OrganizationUnitId id, Guid tenantId, OrganizationUnitId? parentId, string name, HierarchyPath path) : base(id)
    {
        TenantId = tenantId;
        ParentId = parentId;
        Name = name;
        HierarchyPath = path;
    }

    public static OrganizationUnit Create(Guid tenantId, OrganizationUnitId? parentId, string name, HierarchyPath path)
    {
        var entity = new OrganizationUnit(OrganizationUnitId.New(), tenantId, parentId, name, path);
        entity.RaiseDomainEvent(new OrganizationUnitCreated(entity.Id.Value, tenantId, name, parentId?.Value));
        return entity;
    }

    public void Move(OrganizationUnitId? newParentId, HierarchyPath newPath)
    {
        var oldParent = ParentId;
        ParentId = newParentId;
        HierarchyPath = newPath;
        RaiseDomainEvent(new OrganizationUnitMoved(Id.Value, TenantId, oldParent?.Value, newParentId?.Value));
    }
}
