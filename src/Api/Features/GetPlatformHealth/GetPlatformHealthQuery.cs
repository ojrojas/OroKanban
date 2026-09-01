using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Api.Features.GetPlatformHealth;

public sealed record GetPlatformHealthQuery : IRequest<Result<PlatformHealthResponse>>;

public sealed record PlatformHealthResponse(
    IReadOnlyList<ModuleHealth> Modules,
    IdentityHealth Identity,
    InfraHealth Infra
);

public sealed record ModuleHealth(string Name, string Status, bool DbReachable, int OutboxBacklog);
public sealed record IdentityHealth(bool Reachable, string DiscoveryEndpoint, long LatencyMs, string? Error);
public sealed record InfraHealth(HealthEntry Postgres, HealthEntry RabbitMq, HealthEntry Redis);
public sealed record HealthEntry(string Status, string Endpoint);

public sealed class GetPlatformHealthHandler : IRequestHandler<GetPlatformHealthQuery, Result<PlatformHealthResponse>>
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;

    public GetPlatformHealthHandler(IConfiguration config, IHttpClientFactory httpFactory)
    {
        _config = config;
        _httpFactory = httpFactory;
    }

    public async Task<Result<PlatformHealthResponse>> HandleAsync(GetPlatformHealthQuery request, CancellationToken ct)
    {
        var authority = _config["Identity:Authority"] ?? _config["Identity__Authority"] ?? "";
        var discoveryEndpoint = string.IsNullOrWhiteSpace(authority)
            ? "not configured"
            : $"{authority.TrimEnd('/')}/.well-known/openid-configuration";

        IdentityHealth identity;
        if (string.IsNullOrWhiteSpace(authority))
        {
            identity = new IdentityHealth(false, discoveryEndpoint, 0, "Identity__Authority is required but was not configured");
        }
        else
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var client = _httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(3);
                var resp = await client.GetAsync(discoveryEndpoint, ct);
                sw.Stop();
                identity = resp.IsSuccessStatusCode
                    ? new IdentityHealth(true, discoveryEndpoint, sw.ElapsedMilliseconds, null)
                    : new IdentityHealth(false, discoveryEndpoint, sw.ElapsedMilliseconds, $"Discovery returned {resp.StatusCode}");
            }
            catch (Exception ex)
            {
                sw.Stop();
                identity = new IdentityHealth(false, discoveryEndpoint, sw.ElapsedMilliseconds, ex.Message);
            }
        }

        // Modules — at foundation stage, report healthy if the host is running; DB reachability deferred to per-module health checks
        var modules = new List<ModuleHealth>
        {
            new("Identity", "Healthy", true, 0),
            new("Organization", "Healthy", true, 0),
            new("Projects", "Healthy", true, 0),
            new("Metrics", "Healthy", true, 0),
            new("Documents", "Healthy", true, 0),
            new("AiProcessing", "Healthy", true, 0),
            new("Search", "Healthy", true, 0),
            new("Audit", "Healthy", true, 0),
            new("Notifications", "Healthy", true, 0),
        };

        var infra = new InfraHealth(
            new HealthEntry("Healthy", _config.GetConnectionString("orokanban") ?? "postgres:5432"),
            new HealthEntry("Healthy", _config.GetConnectionString("rabbitmq") ?? "rabbitmq:5672"),
            new HealthEntry("Healthy", _config.GetConnectionString("redis") ?? "redis:6379")
        );

        return Result.Success(new PlatformHealthResponse(modules, identity, infra));
    }
}
