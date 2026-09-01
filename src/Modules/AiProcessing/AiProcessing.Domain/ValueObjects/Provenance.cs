using BuildingBlocks.Kernel.Domain.ValueObjects;
using BuildingBlocks.Kernel.Domain.Rules;
using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Domain.ValueObjects;

public sealed class Provenance : ValueObject
{
    public Guid SourceDocumentId { get; }
    public Guid SourceDocumentVersionId { get; }
    public Guid OperationId { get; }
    public string OperationType { get; }
    public ModelDescriptor Model { get; }
    public string PromptVersion { get; }
    public DateTime CreatedAt { get; }
    public Guid CreatedBy { get; }
    public string ProcessingStatus { get; }
    public QualityIndicator? QualityIndicator { get; }

    public Provenance(Guid sourceDocumentId, Guid sourceDocumentVersionId, Guid operationId, string operationType, ModelDescriptor model, string promptVersion, DateTime createdAt, Guid createdBy, string processingStatus, QualityIndicator? qualityIndicator = null)
    {
        if (sourceDocumentId == Guid.Empty) throw new ArgumentException("SourceDocumentId required");
        if (sourceDocumentVersionId == Guid.Empty) throw new ArgumentException("SourceDocumentVersionId required");
        if (operationId == Guid.Empty) throw new ArgumentException("OperationId required");
        if (string.IsNullOrWhiteSpace(operationType)) throw new ArgumentException("OperationType required");
        if (string.IsNullOrWhiteSpace(promptVersion)) throw new ArgumentException("PromptVersion required");
        if (string.IsNullOrWhiteSpace(processingStatus)) throw new ArgumentException("ProcessingStatus required");
        SourceDocumentId = sourceDocumentId;
        SourceDocumentVersionId = sourceDocumentVersionId;
        OperationId = operationId;
        OperationType = operationType;
        Model = model ?? throw new ArgumentNullException(nameof(model));
        PromptVersion = promptVersion;
        CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime();
        CreatedBy = createdBy == Guid.Empty ? throw new ArgumentException("CreatedBy required") : createdBy;
        ProcessingStatus = processingStatus;
        QualityIndicator = qualityIndicator;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return SourceDocumentId;
        yield return SourceDocumentVersionId;
        yield return OperationId;
        yield return OperationType;
        yield return Model;
        yield return PromptVersion;
        yield return CreatedAt;
        yield return CreatedBy;
        yield return ProcessingStatus;
        yield return QualityIndicator;
    }
}
