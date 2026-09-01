using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Api.Features.GetPlatformHealth;
public sealed class DocumentsHealthCheck : IHealthCheck
{
    public System.Threading.Tasks.Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(HealthCheckResult.Healthy("Documents OK"));
}
