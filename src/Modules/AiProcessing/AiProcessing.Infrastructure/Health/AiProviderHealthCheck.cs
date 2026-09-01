namespace AiProcessing.Infrastructure.Health;

public sealed class AiProviderHealthCheck
{
    public Task<string> CheckHealthAsync(CancellationToken ct = default)
    {
        return Task.FromResult("Healthy (InMemory dev)");
    }
}
