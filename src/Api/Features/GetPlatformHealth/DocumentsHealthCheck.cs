using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Api.Features.GetPlatformHealth;
public sealed class DocumentsHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct) => Task.FromResult(HealthCheckResult.Healthy("Documents OK"));
}
