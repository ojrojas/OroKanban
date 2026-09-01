using BuildingBlocks.Kernel.Domain.Entities;

using Documents.Domain.Ids;

namespace Documents.Domain.Entities;

public sealed class DocumentExplicitGrant : Entity<Guid>
{
    public DocumentId DocumentId { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public Guid GranteeUserId { get; private set; }
    public Guid GrantedBy { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    private DocumentExplicitGrant() : base(Guid.NewGuid()) { }

    public DocumentExplicitGrant(DocumentId docId, Guid tenantId, Guid granteeUserId, Guid grantedBy, DateTime? expiresAt = null) : base(Guid.NewGuid())
    {
        DocumentId = docId;
        TenantId = tenantId;
        GranteeUserId = granteeUserId;
        GrantedBy = grantedBy;
        GrantedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired(DateTime now)
    {
        if (RevokedAt is not null) return true;
        if (ExpiresAt is null) return false;
        return now >= ExpiresAt.Value;
    }

    public void Revoke()
    {
        RevokedAt = DateTime.UtcNow;
    }
}
