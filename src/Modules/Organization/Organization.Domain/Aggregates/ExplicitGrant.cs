using BuildingBlocks.Kernel.Domain.Entities;

using Organization.Domain.Events;
using Organization.Domain.ValueObjects;

namespace Organization.Domain.Aggregates;

public sealed class ExplicitGrant : AggregateRoot<ExplicitGrantId>
{
    public Guid TenantId { get; private set; }
    public Guid GranteeUserId { get; private set; }
    public Guid GrantedBy { get; private set; }
    public string ResourceType { get; private set; } = default!;
    public Guid ResourceId { get; private set; }
    public string Permission { get; private set; } = default!;
    public DateTime? ExpiresAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private ExplicitGrant() { }

    private ExplicitGrant(ExplicitGrantId id, Guid tenantId, Guid granteeUserId, Guid grantedBy, string resourceType, Guid resourceId, string permission, DateTime? expiresAt)
        : base(id)
    {
        TenantId = tenantId;
        GranteeUserId = granteeUserId;
        GrantedBy = grantedBy;
        ResourceType = resourceType;
        ResourceId = resourceId;
        Permission = permission;
        ExpiresAt = expiresAt;
    }

    public static ExplicitGrant Issue(Guid tenantId, Guid granteeUserId, Guid grantedBy, string resourceType, Guid resourceId, string permission, DateTime? expiresAt)
    {
        var entity = new ExplicitGrant(ExplicitGrantId.New(), tenantId, granteeUserId, grantedBy, resourceType, resourceId, permission, expiresAt);
        entity.RaiseDomainEvent(new GrantIssued(entity.Id.Value, granteeUserId, resourceType, resourceId, permission, tenantId, expiresAt));
        return entity;
    }

    public void Revoke()
    {
        RaiseDomainEvent(new GrantRevoked(Id.Value, TenantId));
    }

    public bool IsExpired(DateTime now) => ExpiresAt != null && now > ExpiresAt;

    public bool IsSatisfiedBy(Guid tenantId, Guid actorUserId, string resourceType, Guid resourceId, string permission, DateTime now)
    {
        if (IsExpired(now)) return false;
        if (TenantId != tenantId) return false;
        if (GranteeUserId != actorUserId) return false;
        if (!ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase)) return false;
        if (ResourceId != resourceId) return false;
        if (!Permission.Equals(permission, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}