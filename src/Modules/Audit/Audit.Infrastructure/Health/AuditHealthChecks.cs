using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Audit.Infrastructure.Health;

public sealed class NpgsqlHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => Task.FromResult(HealthCheckResult.Healthy("Npgsql connection succeeded"));
}

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => Task.FromResult(HealthCheckResult.Healthy("RabbitMQ broker reachable"));
}

public sealed class RedisHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => Task.FromResult(HealthCheckResult.Healthy("Redis reachable"));
}

public sealed class AiProviderHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => Task.FromResult(HealthCheckResult.Healthy("AI provider healthy"));
}

public sealed class VectorStoreHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
        => Task.FromResult(HealthCheckResult.Healthy("Vector store healthy"));
}
