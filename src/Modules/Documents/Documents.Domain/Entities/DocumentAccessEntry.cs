using BuildingBlocks.Kernel.Domain.Entities;

using Documents.Domain.Ids;

namespace Documents.Domain.Entities;

public sealed class DocumentAccessEntry : Entity<Guid>
{
    public DocumentId DocumentId { get; private set; } = default!;
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = default!; // Read|Download|Denied
    public bool Granted { get; private set; }
    public string ClassificationValue { get; private set; } = default!;
    public string RuleVersion { get; private set; } = default!;
    public string? Reason { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private DocumentAccessEntry() : base(Guid.NewGuid()) { }

    public DocumentAccessEntry(
        DocumentId documentId,
        Guid tenantId,
        Guid actorId,
        string action,
        bool granted,
        string classificationValue,
        string ruleVersion,
        string? reason,
        string? ipAddress = null,
        string? userAgent = null) : base(Guid.NewGuid())
    {
        DocumentId = documentId;
        TenantId = tenantId;
        ActorId = actorId;
        Action = action;
        Granted = granted;
        ClassificationValue = classificationValue;
        RuleVersion = ruleVersion;
        Reason = reason;
        Timestamp = DateTime.UtcNow;
        IpAddress = ipAddress;
        UserAgent = userAgent;
    }
}
