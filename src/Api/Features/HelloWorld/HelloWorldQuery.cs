using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

namespace Api.Features.HelloWorld;

public sealed record HelloWorldQuery : IRequest<Result<HelloWorldResponse>>;

public sealed record HelloWorldResponse(
    string Message,
    string UserId,
    string? TenantId,
    string? Email,
    IReadOnlyList<string> Roles,
    DateTime ServerTimeUtc
);

public sealed class HelloWorldHandler : IRequestHandler<HelloWorldQuery, Result<HelloWorldResponse>>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HelloWorldHandler(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public Task<Result<HelloWorldResponse>> HandleAsync(HelloWorldQuery request, CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return Task.FromResult(Result.Failure<HelloWorldResponse>(Error.Unauthorized("HelloWorld.NotAuthenticated", "User is not authenticated")));

        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var tenantId = user.FindFirst("tenant_id")?.Value ?? user.FindFirst("tenantId")?.Value;
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var roles = user.FindAll("role").Select(c => c.Value)
            .Concat(user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))
            .Distinct().ToList();

        var response = new HelloWorldResponse(
            Message: $"Hello World — authenticated as {sub}",
            UserId: sub,
            TenantId: tenantId,
            Email: email,
            Roles: roles,
            ServerTimeUtc: DateTime.UtcNow
        );

        return Task.FromResult(Result.Success(response));
    }
}
