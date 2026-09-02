using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace Api.Features.Search;

public sealed class GlobalSearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", async (HttpContext ctx, ISender sender, string? q, string? type, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var query = new GlobalSearchQuery(tenantId, q ?? "", type);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record GlobalSearchQuery(Guid TenantId, string Search, string? Type) : IQuery<Result<GlobalSearchResponse>>;

public sealed record GlobalSearchResponse(IReadOnlyList<SearchResultItem> Items);

public sealed record SearchResultItem(string Type, Guid Id, string Title, string? Description, DateTime UpdatedAt);

public sealed class GlobalSearchHandler : IQueryHandler<GlobalSearchQuery, Result<GlobalSearchResponse>>
{
    public Task<Result<GlobalSearchResponse>> HandleAsync(GlobalSearchQuery q, CancellationToken ct)
    {
        return Task.FromResult(Result.Success(new GlobalSearchResponse([])));
    }
}
