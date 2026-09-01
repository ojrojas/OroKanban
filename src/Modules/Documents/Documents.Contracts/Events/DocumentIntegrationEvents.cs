using BuildingBlocks.EventBus.Abstractions;

namespace Documents.Contracts.Events;

public sealed record DocumentUploadedIntegrationEvent(
    Guid DocumentId,
    Guid DocumentVersionId,
    Guid TenantId,
    Guid OwnerId,
    Guid? ProjectId,
    string ContentHash,
    string Classification,
    string RuleVersion) : IntegrationEvent;

public sealed record DocumentVersionPublishedIntegrationEvent(
    Guid DocumentId,
    Guid DocumentVersionId,
    int VersionNumber,
    string ContentHash,
    Guid PublishedBy,
    string RuleVersion,
    Guid? SupersededVersionId) : IntegrationEvent;

public sealed record DocumentClassifiedIntegrationEvent(
    Guid DocumentId,
    string Classification,
    string RuleVersion,
    Guid ActorId) : IntegrationEvent;

public sealed record DocumentAccessedIntegrationEvent(
    Guid DocumentId,
    Guid ActorId,
    string Classification,
    string RuleVersion,
    string Action) : IntegrationEvent;

public sealed record DocumentAccessDeniedIntegrationEvent(
    Guid DocumentId,
    Guid ActorId,
    string Reason,
    string Classification,
    string RuleVersion,
    string Action) : IntegrationEvent;

public sealed record DocumentDeletedIntegrationEvent(
    Guid DocumentId,
    Guid ActorId,
    DateTime DeletedAt,
    Guid TenantId) : IntegrationEvent;

public sealed record DocumentApprovedIntegrationEvent(
    Guid DocumentId,
    Guid ApproverId,
    DateTime ApprovedAt) : IntegrationEvent;

public sealed record DocumentIndexedIntegrationEvent(
    Guid DocumentId,
    Guid DocumentVersionId,
    string ContentHash,
    string MimeType,
    string MetadataSnapshotJson,
    string Classification,
    string RuleVersion,
    Guid TenantId) : IntegrationEvent;

public sealed record DocumentProcessingStageCompletedIntegrationEvent(
    Guid JobId,
    Guid DocumentId,
    string Stage) : IntegrationEvent;

public sealed record DocumentProcessingFailedIntegrationEvent(
    Guid JobId,
    Guid DocumentId,
    string Stage,
    string Reason,
    bool Retryable,
    int AttemptCount) : IntegrationEvent;

public sealed record DocumentProcessingStageRequestedIntegrationEvent(
    Guid JobId,
    Guid DocumentId,
    Guid DocumentVersionId,
    string Stage,
    Guid TenantId) : IntegrationEvent;
