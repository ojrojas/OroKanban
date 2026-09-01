using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace AiProcessing.Domain.ValueObjects;

public sealed class ChunkReference : ValueObject
{
    public Guid DocumentId { get; }
    public Guid DocumentVersionId { get; }
    public int ChunkId { get; }
    public Guid TenantId { get; }
    public string Classification { get; }
    public float? Score { get; }

    public ChunkReference(Guid documentId, Guid documentVersionId, int chunkId, Guid tenantId, string classification, float? score = null)
    {
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId required");
        if (documentVersionId == Guid.Empty) throw new ArgumentException("DocumentVersionId required");
        if (chunkId < 0) throw new ArgumentException("ChunkId >=0");
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId required");
        if (string.IsNullOrWhiteSpace(classification)) throw new ArgumentException("Classification required");
        DocumentId = documentId;
        DocumentVersionId = documentVersionId;
        ChunkId = chunkId;
        TenantId = tenantId;
        Classification = classification;
        Score = score;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DocumentId;
        yield return DocumentVersionId;
        yield return ChunkId;
        yield return TenantId;
        yield return Classification;
    }
}
