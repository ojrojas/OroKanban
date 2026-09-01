namespace AiProcessing.Infrastructure.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "AI";
    public string Provider { get; set; } = "inmemory"; // azure|openai|ollama|inmemory
    public string ModelId { get; set; } = "gpt-4o-2024-08-06";
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public int TokenBudget { get; set; } = 8000;
    public int MaxRetries { get; set; } = 3;
    public float Temperature { get; set; } = 0f;
    public int MaxOutputTokens { get; set; } = 1024;
}

public sealed class VectorStoreOptions
{
    public const string SectionName = "VectorStore";
    public string Provider { get; set; } = "inmemory"; // qdrant|pgvector|inmemory
    public string? ConnectionString { get; set; }
    public string CollectionName { get; set; } = "ai_chunks";
}
