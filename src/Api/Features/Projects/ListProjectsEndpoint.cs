using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Projects.Infrastructure.Persistence;
using Projects.Domain.Enumerations;

using Microsoft.EntityFrameworkCore;

namespace Api.Features.Projects;

public sealed class ListProjectsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/projects", async (HttpContext ctx, ISender sender, int? page, int? pageSize, string? q, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListProjectsQuery(tenantId, page ?? 1, pageSize ?? 20, q), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListProjectsQuery(Guid TenantId, int Page, int PageSize, string? Search) : IQuery<Result<PagedResponse>>;

public sealed record PagedResponse(IReadOnlyList<ProjectListItem> Items, int Total, int Page, int PageSize);
public sealed record ProjectListItem(Guid Id, string Name, string? Description, string Status, string Priority, Guid OwnerId, DateTime CreatedAt, DateTime UpdatedAt);

public sealed class ListProjectsHandler(ProjectsDbContext db) : IQueryHandler<ListProjectsQuery, Result<PagedResponse>>
{
    public async Task<Result<PagedResponse>> HandleAsync(ListProjectsQuery q, CancellationToken ct)
    {
        var query = db.Projects.AsNoTracking().Where(p => p.TenantId == q.TenantId);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(p => p.Name.Contains(q.Search));
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var items = await query.OrderByDescending(p => p.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new ProjectListItem(p.Id.Value, p.Name, p.Description,
                ProjectStatus.FromId(p.StatusId).Name,
                ProjectPriority.FromId(p.PriorityId).Name,
                p.OwnerId, p.CreatedAt, p.UpdatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedResponse(items, total, page, pageSize));
    }
}
