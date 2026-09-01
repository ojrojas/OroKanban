using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using Documents.Domain.Enumerations;
using Documents.Domain.Events;
using Documents.Domain.Ids;
using Documents.Domain.Rules;
using Documents.Domain.ValueObjects;

namespace Documents.Domain.Aggregates;

public sealed class DocumentVersion : AggregateRoot<DocumentVersionId>
{
    public DocumentId DocumentId { get; private set; } = default!;
    public int VersionNumber { get; private set; }
    public string ContentHash { get; private set; } = default!;
    public string MimeType { get; private set; } = default!;
    public long Size { get; private set; }
    public bool IsPublished { get; private set; }
    public DateTime PublishedAt { get; private set; }
    public Guid PublishedBy { get; private set; }
    public string RuleVersion { get; private set; } = default!;
    public string MetadataSnapshotJson { get; private set; } = default!;
    public DateTime? MetadataEffectiveDate { get; private set; }
    public DateTime? MetadataExpirationDate { get; private set; }
    public bool IsSafe { get; private set; }
    public int ScanStatusId { get; private set; }
    public DateTime? ScannedAt { get; private set; }
    public Guid? ScannedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private DocumentVersion() { }

    private DocumentVersion(
        DocumentVersionId id,
        DocumentId documentId,
        int versionNumber,
        string contentHash,
        string mimeType,
        long size,
        Guid publishedBy,
        string ruleVersion,
        string metadataJson,
        DateTime? effectiveDate,
        DateTime? expirationDate)
    {
        Id = id;
        DocumentId = documentId;
        VersionNumber = versionNumber;
        ContentHash = contentHash;
        MimeType = mimeType;
        Size = size;
        IsPublished = true;
        PublishedAt = DateTime.UtcNow;
        PublishedBy = publishedBy;
        RuleVersion = ruleVersion;
        MetadataSnapshotJson = metadataJson;
        MetadataEffectiveDate = effectiveDate;
        MetadataExpirationDate = expirationDate;
        IsSafe = false;
        ScanStatusId = ScanStatus.Pending.Id;
        CreatedAt = PublishedAt;
    }

    public static DocumentVersion Create(
        DocumentId documentId,
        int versionNumber,
        string contentHash,
        string mimeType,
        long size,
        Guid publishedBy,
        string ruleVersion,
        string metadataJson,
        DateTime? effectiveDate,
        DateTime? expirationDate)
    {
        var v = new DocumentVersion(DocumentVersionId.New(), documentId, versionNumber, contentHash, mimeType, size, publishedBy, ruleVersion, metadataJson, effectiveDate, expirationDate);
        v.RaiseDomainEvent(new DocumentVersionPublishedDomainEvent(v.Id, documentId, versionNumber, contentHash, publishedBy, ruleVersion));
        return v;
    }

    public void MarkSafe(DateTime scannedAt, Guid? scannedBy)
    {
        if (IsSafe && ScanStatusId == ScanStatus.Safe.Id) return; // idempotent
        if (ScanStatusId != ScanStatus.Pending.Id && ScanStatusId != ScanStatus.Unavailable.Id)
        {
            // Allow Pending -> Safe; Infected stays Infected per spec — MarkSafe only from Pending/Unavailable
            // If already Infected, don't override
            if (ScanStatusId == ScanStatus.Infected.Id) return;
        }
        IsSafe = true;
        ScanStatusId = ScanStatus.Safe.Id;
        ScannedAt = scannedAt;
        ScannedBy = scannedBy;
        RaiseDomainEvent(new DocumentVersionMarkedSafeDomainEvent(Id, scannedAt, scannedBy));
    }

    public void MarkInfected(string reason, DateTime scannedAt, Guid? scannedBy)
    {
        IsSafe = false;
        ScanStatusId = ScanStatus.Infected.Id;
        ScannedAt = scannedAt;
        ScannedBy = scannedBy;
        RaiseDomainEvent(new DocumentVersionScanFailedDomainEvent(Id, reason, ScanStatus.Infected.Name));
    }

    public void MarkUnavailable(string reason, DateTime scannedAt)
    {
        IsSafe = false;
        ScanStatusId = ScanStatus.Unavailable.Id;
        ScannedAt = scannedAt;
        RaiseDomainEvent(new DocumentVersionScanFailedDomainEvent(Id, reason, ScanStatus.Unavailable.Name));
    }

    // Guard for immutability
    public void EnsureMutable()
    {
        CheckRule(new VersionIsImmutableOncePublishedRule(IsPublished));
    }
}
