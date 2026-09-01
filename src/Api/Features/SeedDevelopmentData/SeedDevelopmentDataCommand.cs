using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Api.Features.SeedDevelopmentData;

public sealed record SeedDevelopmentDataCommand(
    string OrganizationName,
    string AdminEmail
) : IRequest<Result<SeedDevelopmentDataResponse>>;

public sealed record SeedDevelopmentDataResponse(
    string OrganizationName,
    string Message
);

public sealed class SeedDevelopmentDataHandler : IRequestHandler<SeedDevelopmentDataCommand, Result<SeedDevelopmentDataResponse>>
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IWebHostEnvironment _env;

    public SeedDevelopmentDataHandler(IConfiguration config, IHttpClientFactory httpFactory, IWebHostEnvironment env)
    {
        _config = config;
        _httpFactory = httpFactory;
        _env = env;
    }

    public async Task<Result<SeedDevelopmentDataResponse>> HandleAsync(SeedDevelopmentDataCommand cmd, CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            return Result.Failure<SeedDevelopmentDataResponse>(Error.Validation("SeedDevelopmentData", "Seed is only available in Development environment"));
        }

        var authority = _config["Identity:Authority"] ?? _config["Identity__Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            return Result.Failure<SeedDevelopmentDataResponse>(Error.Validation("Identity__Authority", "Identity__Authority is required but was not configured"));
        }

        // In a full implementation, this would call OroIdentityServer admin APIs:
        // POST /api/tenants, POST /api/users, PUT /api/users/{id}/roles
        // For foundation, we return a placeholder that proves the wiring without requiring a live identity server.
        await Task.Delay(10, ct);
        return Result.Success(new SeedDevelopmentDataResponse(cmd.OrganizationName, $"Seed placeholder for {cmd.OrganizationName} ({cmd.AdminEmail}) — wired to {authority} (dev-only)"));
    }
}