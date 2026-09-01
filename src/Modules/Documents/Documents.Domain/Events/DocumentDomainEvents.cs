using BuildingBlocks.Kernel.Domain.Events;

using Documents.Domain.Ids;

namespace Documents.Domain.Events;

public abstract record DocumentDomainEvent : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}

public sealed record DocumentUploadedDomainEvent(
    DocumentId DocumentId,
    DocumentVersionId VersionId,
    Guid TenantId,
    Guid OwnerId,
    Guid? ProjectId,
    string ContentHash,
    string Name,
    string Classification,
    string RuleVersion) : DocumentDomainEvent;

public sealed record DocumentValidatedDomainEvent(DocumentId DocumentId) : DocumentDomainEvent;
public sealed record DocumentMarkedSafeDomainEvent(DocumentId DocumentId, DocumentVersionId VersionId, DateTime ScannedAt) : DocumentDomainEvent;
public sealed record DocumentScanFailedDomainEvent(DocumentId DocumentId, DocumentVersionId VersionId, string Reason, string ScanStatus) : DocumentDomainEvent;
public sealed record DocumentClassifiedDomainEvent(DocumentId DocumentId, string Classification, string RuleVersion, Guid ActorId) : DocumentDomainEvent;
public sealed record DocumentAccessedDomainEvent(DocumentId DocumentId, Guid ActorId, string Classification, string RuleVersion, string Action) : DocumentDomainEvent;
public sealed record DocumentAccessDeniedDomainEvent(DocumentId DocumentId, Guid ActorId, string Reason, string Classification, string RuleVersion, string Action) : DocumentDomainEvent;
public sealed record DocumentDeletedDomainEvent(DocumentId DocumentId, Guid ActorId, DateTime DeletedAt) : DocumentDomainEvent;
public sealed record DocumentApprovedDomainEvent(DocumentId DocumentId, Guid ApproverId, DateTime ApprovedAt, string FromStatus, string ToStatus) : DocumentDomainEvent;
public sealed record DocumentVersionPublishedDomainEvent(DocumentVersionId VersionId, DocumentId DocumentId, int VersionNumber, string ContentHash, Guid PublishedBy, string RuleVersion) : DocumentDomainEvent;
public sealed record DocumentVersionSupersededDomainEvent(DocumentVersionId VersionId, DocumentVersionId SupersededByVersionId) : DocumentDomainEvent;
public sealed record DocumentProcessingStageCompletedDomainEvent(Guid JobId, string Stage) : DocumentDomainEvent;
public sealed record DocumentProcessingFailedDomainEvent(Guid JobId, string Stage, string Reason, bool Retryable, int AttemptCount) : DocumentDomainEvent;
public sealed record DocumentVersionMarkedSafeDomainEvent(DocumentVersionId VersionId, DateTime ScannedAt, Guid? ScannedBy) : DocumentDomainEvent;
public sealed record DocumentVersionScanFailedDomainEvent(DocumentVersionId VersionId, string Reason, string ScanStatus) : DocumentDomainEvent;
