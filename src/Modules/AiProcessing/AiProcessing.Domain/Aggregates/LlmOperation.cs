using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.Kernel.Domain.Entities;
using AiProcessing.Domain.Ids;
using AiProcessing.Domain.Enumerations;
using AiProcessing.Domain.ValueObjects;
using AiProcessing.Domain.Events;
using AiProcessing.Domain.Rules;

namespace AiProcessing.Domain.Aggregates;

public sealed class LlmOperation : AggregateRoot<LlmOperationId>
{
    public Guid TenantId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid DocumentVersionId { get; private set; }
    public int OperationTypeId { get; private set; }
    public int OperationStatusId { get; private set; }
    public string ModelProvider { get; private set; } = default!;
    public string ModelName { get; private set; } = default!;
    public string ModelVersion { get; private set; } = default!;
    public LlmPromptVersionId PromptVersionId { get; private set; } = default!;
    public Guid CorrelationId { get; private set; }
    public string StageStatusesJson { get; private set; } = "{}";
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public int? LastErrorStage { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = default!;

    private LlmOperation() { }

    public LlmOperation(LlmOperationId id, Guid tenantId, Guid documentId, Guid documentVersionId, int operationTypeId, ModelDescriptor model, LlmPromptVersionId promptVersionId, Guid createdBy)
    {
        Id = id;
        TenantId = tenantId;
        DocumentId = documentId;
        DocumentVersionId = documentVersionId;
        OperationTypeId = operationTypeId;
        OperationStatusId = OperationStatus.Queued.Id;
        ModelProvider = model.Provider;
        ModelName = model.ModelName;
        ModelVersion = model.Version;
        PromptVersionId = promptVersionId;
        CorrelationId = id.Value;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        StageStatusesJson = "{}";
        RaiseDomainEvent(new LlmOperationQueuedDomainEvent(id, documentId, documentVersionId, operationTypeId.ToString(), ModelName, promptVersionId.Value.ToString(), tenantId, CorrelationId));
    }

    public void MarkFailed(string stage, string reason, bool retryable)
    {
        OperationStatusId = retryable ? OperationStatus.FailedRetryable.Id : OperationStatus.FailedPermanent.Id;
        LastError = reason;
        AttemptCount++;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new LlmOperationFailedDomainEvent(Id, stage, reason, retryable, AttemptCount));
    }

    public Result MarkSucceeded()
    {
        OperationStatusId = OperationStatus.Completed.Id;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        return Result.Success();
    }

    public void Retry()
    {
        CheckRule(new StageIsRetryableRule(OperationStatus.FromId(OperationStatusId).Name));
        OperationStatusId = OperationStatus.Queued.Id;
        AttemptCount++;
        UpdatedAt = DateTime.UtcNow;
        RaiseDomainEvent(new LlmOperationRetriedDomainEvent(Id, "Retry", AttemptCount));
    }
}
