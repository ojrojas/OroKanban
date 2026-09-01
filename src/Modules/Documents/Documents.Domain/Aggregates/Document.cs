using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using Documents.Domain.Enumerations;
using Documents.Domain.Events;
using Documents.Domain.Ids;
using Documents.Domain.Rules;
using Documents.Domain.ValueObjects;

namespace Documents.Domain.Aggregates;

public sealed class Document : AggregateRoot<DocumentId>
{
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Name { get; private set; } = default!;
    public string ClassificationValue { get; private set; } = default!;
    public int ClassificationLevelId { get; private set; }
    public string RuleVersion { get; private set; } = default!;
    public DocumentVersionId CurrentVersionId { get; private set; } = default!;
    public DocumentStatus Status { get; private set; } = default!;
    public string MimeType { get; private set; } = default!;
    public long Size { get; private set; }
    public string ContentHash { get; private set; } = default!;
    public Guid? ProjectId { get; private set; }
    public Guid? WorkItemId { get; private set; }
    public string ProvenanceSource { get; private set; } = default!;
    public string OriginalFilename { get; private set; } = default!;
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public DateTime? RetentionRetainUntil { get; private set; }
    public int? RetentionDays { get; private set; }
    public bool RetentionLegalHold { get; private set; }
    public bool IsSafe { get; private set; }
    public int ScanStatusId { get; private set; }
    public DateTime? ScannedAt { get; private set; }
    public Guid? ScannedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private Document() { }

    private Document(
        DocumentId id,
        Guid tenantId,
        Guid organizationId,
        Guid ownerId,
        string name,
        string classificationValue,
        int classificationLevelId,
        string ruleVersion,
        DocumentVersionId currentVersionId,
        string mimeType,
        long size,
        string contentHash,
        Guid? projectId,
        Guid? workItemId,
        string provenanceSource,
        string originalFilename,
        Guid createdBy)
    {
        Id = id;
        TenantId = tenantId;
        OrganizationId = organizationId;
        OwnerId = ownerId;
        Name = name;
        ClassificationValue = classificationValue;
        ClassificationLevelId = classificationLevelId;
        RuleVersion = ruleVersion;
        CurrentVersionId = currentVersionId;
        Status = DocumentStatus.Uploaded;
        MimeType = mimeType;
        Size = size;
        ContentHash = contentHash;
        ProjectId = projectId;
        WorkItemId = workItemId;
        ProvenanceSource = provenanceSource;
        OriginalFilename = originalFilename;
        CreatedBy = createdBy;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        IsSafe = false;
        ScanStatusId = ScanStatus.Pending.Id;
    }

    public static Result<Document> Create(
        Guid tenantId,
        Guid organizationId,
        Guid ownerId,
        string name,
        string classificationValue,
        int classificationLevelId,
        string ruleVersion,
        DocumentVersionId currentVersionId,
        string mimeType,
        long size,
        string contentHash,
        Guid? projectId,
        Guid? workItemId,
        string provenanceSource,
        string originalFilename)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 300)
            return Result.Failure<Document>(Error.Validation("Document.NameInvalid", "Name must be 1..300 chars."));
        if (string.IsNullOrWhiteSpace(classificationValue))
            return Result.Failure<Document>(Error.Validation("Document.ClassificationRequired", "Classification is required."));
        var doc = new Document(
            DocumentId.New(), tenantId, organizationId, ownerId, name.Trim(),
            classificationValue, classificationLevelId, ruleVersion, currentVersionId,
            mimeType, size, contentHash, projectId, workItemId, provenanceSource, originalFilename, ownerId);
        doc.RaiseDomainEvent(new DocumentUploadedDomainEvent(doc.Id, currentVersionId, tenantId, ownerId, projectId, contentHash, name, classificationValue, ruleVersion));
        return Result.Success(doc);
    }

    public Result ChangeStatus(DocumentStatus target)
    {
        var rule = new DocumentStatusTransitionRule(Status, target);
        if (rule.IsBroken())
            return Result.Failure(Error.Failure("Document.StatusTransition", rule.Message));
        Status = target;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void MarkSafe(DateTime scannedAt, Guid? scannedBy)
    {
        IsSafe = true;
        ScanStatusId = ScanStatus.Safe.Id;
        ScannedAt = scannedAt;
        ScannedBy = scannedBy;
        UpdatedAt = scannedAt;
        RaiseDomainEvent(new DocumentMarkedSafeDomainEvent(Id, CurrentVersionId, scannedAt));
    }

    public void MarkScanFailed(string reason, string scanStatusName)
    {
        IsSafe = false;
        var status = scanStatusName == nameof(ScanStatus.Infected) ? ScanStatus.Infected : ScanStatus.Unavailable;
        ScanStatusId = status.Id;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new DocumentScanFailedDomainEvent(Id, CurrentVersionId, reason, scanStatusName));
    }

    public Result Delete(Guid actorId)
    {
        var target = DocumentStatus.Deleted;
        var rule = new DocumentStatusTransitionRule(Status, target);
        if (rule.IsBroken())
            return Result.Failure(Error.Failure("Document.DeleteTransition", rule.Message));
        Status = target;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = actorId;
        UpdatedAt = DeletedAt.Value;
        RaiseDomainEvent(new DocumentDeletedDomainEvent(Id, actorId, DeletedAt.Value));
        return Result.Success();
    }

    public Result Approve(Guid approverId)
    {
        var target = DocumentStatus.Approved;
        var rule = new DocumentStatusTransitionRule(Status, target);
        if (rule.IsBroken())
            return Result.Failure(Error.Failure("Document.ApproveTransition", rule.Message));
        var from = Status.Name;
        Status = target;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new DocumentApprovedDomainEvent(Id, approverId, UpdatedAt, from, target.Name));
        return Result.Success();
    }

    public Result Reclassify(string newClassification, int newLevelId, string newRuleVersion, Guid actorId)
    {
        if (string.IsNullOrWhiteSpace(newClassification))
            return Result.Failure(Error.Validation("Document.ClassificationRequired", "Classification is required."));
        ClassificationValue = newClassification;
        ClassificationLevelId = newLevelId;
        RuleVersion = newRuleVersion;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new DocumentClassifiedDomainEvent(Id, newClassification, newRuleVersion, actorId));
        return Result.Success();
    }

    public void UpdateCurrentVersion(DocumentVersionId newVersionId)
    {
        CurrentVersionId = newVersionId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateContent(string contentHash, string mimeType, long size)
    {
        ContentHash = contentHash;
        MimeType = mimeType;
        Size = size;
        UpdatedAt = DateTime.UtcNow;
    }
}
