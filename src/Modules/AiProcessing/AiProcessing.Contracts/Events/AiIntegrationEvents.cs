using BuildingBlocks.EventBus.Abstractions;

namespace AiProcessing.Contracts.Events;

public sealed record LlmOperationQueuedIntegrationEvent(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, int OperationTypeId, string ModelProvider, string ModelName, string PromptVersion, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationCompletedIntegrationEvent(Guid OperationId, Guid ResultId, int OperationTypeId, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationFailedIntegrationEvent(Guid OperationId, string Stage, string Reason, bool Retryable, int AttemptCount, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmOperationRetriedIntegrationEvent(Guid OperationId, string Stage, int AttemptCount, Guid CorrelationId) : IntegrationEvent;
public sealed record PromptVersionPublishedIntegrationEvent(Guid PromptVersionId, int OperationTypeId, int VersionNumber, Guid PublishedBy) : IntegrationEvent;
public sealed record LlmResultGeneratedIntegrationEvent(Guid ResultId, Guid OperationId, Guid DocumentId, int OperationTypeId, string ReviewStatus, string ProvenanceJson, Guid TenantId) : IntegrationEvent;
public sealed record LlmResultApprovedIntegrationEvent(Guid ResultId, Guid ReviewerId, string Rationale) : IntegrationEvent;
public sealed record LlmResultRejectedIntegrationEvent(Guid ResultId, Guid ReviewerId, string Rationale) : IntegrationEvent;
public sealed record LlmResultSupersededIntegrationEvent(Guid ResultId, Guid SupersededByResultId) : IntegrationEvent;
public sealed record LlmReviewCreatedIntegrationEvent(Guid ReviewId, Guid ResultId, Guid ReviewerId, string Decision, string Rationale) : IntegrationEvent;
public sealed record RagQueryExecutedIntegrationEvent(Guid OperationId, string Query, int RetrievedCount, int FilteredOutCount, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
public sealed record LlmProcessingStageCompletedIntegrationEvent(Guid OperationId, string Stage) : IntegrationEvent;
public sealed record LlmProcessingStageRequestedIntegrationEvent(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, string Stage, Guid TenantId, Guid CorrelationId) : IntegrationEvent;
