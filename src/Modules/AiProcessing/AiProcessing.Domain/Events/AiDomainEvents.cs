using BuildingBlocks.Kernel.Domain.Events;
using AiProcessing.Domain.Ids;

namespace AiProcessing.Domain.Events;

public sealed record LlmOperationQueuedDomainEvent(LlmOperationId OperationId, Guid DocumentId, Guid DocumentVersionId, string OperationType, string ModelName, string PromptVersion, Guid TenantId, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmOperationCompletedDomainEvent(LlmOperationId OperationId, LlmResultId ResultId, string OperationType) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmOperationFailedDomainEvent(LlmOperationId OperationId, string Stage, string Reason, bool Retryable, int AttemptCount) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmOperationRetriedDomainEvent(LlmOperationId OperationId, string Stage, int AttemptCount) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record PromptVersionPublishedDomainEvent(LlmPromptVersionId PromptVersionId, string OperationType, int VersionNumber, Guid PublishedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmResultGeneratedDomainEvent(LlmResultId ResultId, LlmOperationId OperationId, Guid DocumentId, string ReviewStatus, string ProvenanceJson) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmResultApprovedDomainEvent(LlmResultId ResultId, Guid ReviewerId, string Rationale) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmResultRejectedDomainEvent(LlmResultId ResultId, Guid ReviewerId, string Rationale) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmResultSupersededDomainEvent(LlmResultId ResultId, LlmResultId SupersededBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record LlmReviewCreatedDomainEvent(LlmReviewId ReviewId, LlmResultId ResultId, Guid ReviewerId, string Decision) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
public sealed record RagQueryExecutedDomainEvent(LlmOperationId OperationId, string Query, int RetrievedCount, int FilteredOutCount, Guid TenantId, Guid CorrelationId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; } = DateTime.UtcNow;
}
