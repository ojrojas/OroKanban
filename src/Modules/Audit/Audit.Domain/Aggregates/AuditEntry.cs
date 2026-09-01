using BuildingBlocks.Kernel.Domain.Entities;
using Audit.Domain.Ids;
using Audit.Domain.Enumerations;
using Audit.Domain.ValueObjects;

namespace Audit.Domain.Aggregates;

public sealed class AuditEntry : AggregateRoot<AuditEntryId>
{
    public DateTime Timestamp { get; private set; }
    public ActorReference Actor { get; private set; } = default!;
    public AuditAction Action { get; private set; } = default!;
    public string ResourceType { get; private set; } = default!;
    public string ResourceId { get; private set; } = default!;
    public Guid? OrganizationId { get; private set; }
    public Guid TenantId { get; private set; }
    public AuditResult Result { get; private set; } = default!;
    public Guid CorrelationId { get; private set; }
    public Guid? ProjectId { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public BeforeAfterSnapshot Snapshot { get; private set; } = default!;
    public string? PreviousHash { get; private set; }
    public string? Hash { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private AuditEntry() { }

    public AuditEntry(AuditEntryId id, DateTime timestamp, ActorReference actor, AuditAction action, string resourceType, string resourceId, Guid? organizationId, Guid tenantId, AuditResult result, Guid correlationId, Guid? projectId, string? ipAddress, string? userAgent, BeforeAfterSnapshot snapshot, string? previousHash, string? hash)
    {
        Id = id;
        Timestamp = timestamp.Kind == DateTimeKind.Utc ? timestamp : timestamp.ToUniversalTime();
        Actor = actor;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        OrganizationId = organizationId;
        TenantId = tenantId;
        Result = result;
        CorrelationId = correlationId;
        ProjectId = projectId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Snapshot = snapshot;
        PreviousHash = previousHash;
        Hash = hash;
    }
    // No public setters, no Update/Delete mutators — immutable by design
}
