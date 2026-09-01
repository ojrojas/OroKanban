using BuildingBlocks.Kernel.Domain.Entities;
using AiProcessing.Domain.Ids;
using AiProcessing.Domain.Events;

namespace AiProcessing.Domain.Aggregates;

public sealed class LlmReview : AggregateRoot<LlmReviewId>
{
    public LlmResultId ResultId { get; private set; } = default!;
    public Guid ReviewerId { get; private set; }
    public Guid TenantId { get; private set; }
    public int Decision { get; private set; } // 1 Approved, 2 Rejected
    public string Rationale { get; private set; } = default!;
    public DateTime ReviewedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private LlmReview() { }

    public LlmReview(LlmReviewId id, LlmResultId resultId, Guid reviewerId, Guid tenantId, int decision, string rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale) || rationale.Length > 2000) throw new ArgumentException("Rationale 1..2000");
        Id = id;
        ResultId = resultId;
        ReviewerId = reviewerId;
        TenantId = tenantId;
        Decision = decision;
        Rationale = rationale;
        ReviewedAt = DateTime.UtcNow;
        RaiseDomainEvent(new LlmReviewCreatedDomainEvent(id, resultId, reviewerId, decision.ToString()));
    }
}
