namespace AiProcessing.Contracts.DTOs;

public sealed record QueueOperationRequest(Guid DocumentId, Guid DocumentVersionId, string OperationType, string? PromptVersionId, ModelDescriptorDto? Model);
public sealed record ModelDescriptorDto(string Provider, string ModelName, string Version);
public sealed record QueueOperationResponse(Guid OperationId, Guid DocumentId, Guid DocumentVersionId, string OperationType, string OperationStatus, Guid CorrelationId, Guid PromptVersionId, int PromptVersionNumber);
public sealed record OperationProvenanceResponse(Guid OperationId, string OperationType, string ModelProvider, string ModelName, string PromptVersion, DateTime CreatedAt, Guid CreatedBy, string ProcessingStatus, object? QualityIndicator);
public sealed record PagedResultEnvelope<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
public sealed record PromptVersionDto(Guid PromptVersionId, string OperationType, int VersionNumber, string Template);
