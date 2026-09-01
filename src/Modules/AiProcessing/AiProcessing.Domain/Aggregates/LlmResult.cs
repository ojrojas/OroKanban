using AiProcessing.Domain.Enumerations;
using BuildingBlocks.Kernel.Domain.Entities;
using AiProcessing.Domain.Ids;
using AiProcessing.Domain.ValueObjects;
using AiProcessing.Domain.Events;
using AiProcessing.Domain.Rules;

namespace AiProcessing.Domain.Aggregates;

public sealed class LlmResult : AggregateRoot<LlmResultId>
{
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public LlmOperationId OperationId { get; private set; } = default!;
    public int OperationTypeId { get; private set; }
    public string ProvenanceJson { get; private set; } = default!;
    public Provenance Provenance { get; private set; } = default!;
    public string Content { get; private set; } = default!;
    public string? ProposedValueJson { get; private set; }
    public string? ChunkReferencesJson { get; private set; }
    public int ReviewStatusId { get; private set; }
    public string? QualityIndicatorJson { get; private set; }
    public Guid? SupersededBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private LlmResult() { }

    public LlmResult(LlmResultId id, Guid tenantId, Guid documentId, Guid documentVersionId, LlmOperationId operationId, int operationTypeId, Provenance provenance, string content, Guid createdBy, bool requiresReview)
    {
        CheckRule(new ProvenanceCompleteRule(provenance != null));
        Id = id;
        TenantId = tenantId;
        DocumentId = documentId;
        DocumentVersionId = documentVersionId;
        OperationId = operationId;
        OperationTypeId = operationTypeId;
        Provenance = provenance!;
        ProvenanceJson = System.Text.Json.JsonSerializer.Serialize(provenance);
        Content = content;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        ReviewStatusId = requiresReview ? ReviewStatus.PendingReview.Id : ReviewStatus.Generated.Id;
        RaiseDomainEvent(new LlmResultGeneratedDomainEvent(id, operationId, documentId, ReviewStatusId.ToString(), ProvenanceJson));
    }

    public void Approve(Guid reviewerId, string rationale)
    {
        CheckRule(new ReviewStatusTransitionRule(ReviewStatus.FromId(ReviewStatusId).Name, ReviewStatus.Approved.Name));
        ReviewStatusId = ReviewStatus.Approved.Id;
        RaiseDomainEvent(new LlmResultApprovedDomainEvent(Id, reviewerId, rationale));
    }

    public void Reject(Guid reviewerId, string rationale)
    {
        CheckRule(new ReviewStatusTransitionRule(ReviewStatus.FromId(ReviewStatusId).Name, ReviewStatus.Rejected.Name));
        ReviewStatusId = ReviewStatus.Rejected.Id;
        RaiseDomainEvent(new LlmResultRejectedDomainEvent(Id, reviewerId, rationale));
    }

    public void MarkSuperseded(LlmResultId supersededBy)
    {
        CheckRule(new ReviewStatusTransitionRule(ReviewStatus.FromId(ReviewStatusId).Name, ReviewStatus.Superseded.Name));
        ReviewStatusId = ReviewStatus.Superseded.Id;
        SupersededBy = supersededBy.Value;
        RaiseDomainEvent(new LlmResultSupersededDomainEvent(Id, supersededBy));
    }
}
