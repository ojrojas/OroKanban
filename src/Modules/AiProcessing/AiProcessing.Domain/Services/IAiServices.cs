using BuildingBlocks.Kernel.Domain.Results;

namespace AiProcessing.Domain.Services;

public interface ILLMProcessor
{
    Task<Result<string>> ProcessAsync(string prompt, string content, string modelId, CancellationToken ct);
}

public interface IDocumentExtractor
{
    Task<Result<string>> ExtractAsync(Guid documentVersionId, CancellationToken ct);
}

public interface IEmbeddingProvider
{
    Task<Result<float[]>> EmbedAsync(string text, CancellationToken ct);
    Task<Result<IReadOnlyList<Guid>>> SearchAsync(float[] queryEmbedding, Guid tenantId, int topK, float minScore, CancellationToken ct);
}

public interface IReviewPolicy
{
    bool RequiresReview(string operationType, string classification);
}

public interface IAuthorizedRetrievalPolicy
{
    Task<Result<IReadOnlyList<ValueObjects.ChunkReference>>> FilteredSearchAsync(float[] queryEmbedding, Guid actorId, Guid tenantId, int topK, float minScore, CancellationToken ct);
}

public interface IResultValidationPolicy
{
    (bool IsInjectionFlagged, string SanitizedOutput) Validate(string rawContent, string llmOutput);
}
