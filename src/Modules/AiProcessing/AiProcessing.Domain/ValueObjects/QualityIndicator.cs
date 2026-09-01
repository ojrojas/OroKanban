using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace AiProcessing.Domain.ValueObjects;

public sealed class QualityIndicator : ValueObject
{
    public float? Confidence { get; }
    public float? QualityScore { get; }
    public bool IsInjectionFlagged { get; }
    public int? ChunkCount { get; }
    public int? TokenCount { get; }
    public float? RelevanceScore { get; }

    public QualityIndicator(float? confidence = null, float? qualityScore = null, bool isInjectionFlagged = false, int? chunkCount = null, int? tokenCount = null, float? relevanceScore = null)
    {
        if (confidence is not null && (confidence < 0 || confidence > 1)) throw new ArgumentException("Confidence 0..1");
        Confidence = confidence;
        QualityScore = qualityScore;
        IsInjectionFlagged = isInjectionFlagged;
        ChunkCount = chunkCount;
        TokenCount = tokenCount;
        RelevanceScore = relevanceScore;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Confidence;
        yield return QualityScore;
        yield return IsInjectionFlagged;
        yield return ChunkCount;
        yield return TokenCount;
        yield return RelevanceScore;
    }
}
