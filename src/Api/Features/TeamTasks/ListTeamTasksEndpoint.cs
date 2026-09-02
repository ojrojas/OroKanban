using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace Api.Features.TeamTasks;

public sealed class ListTeamTasksEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/team-tasks", async (HttpContext ctx, ISender sender, int? page, int? pageSize, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var sg) ? sg : Guid.Empty;
            var query = new ListTeamTasksQuery(tenantId, callerId, page ?? 1, pageSize ?? 20);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListTeamTasksQuery(Guid TenantId, Guid CallerId, int Page, int PageSize) : IQuery<Result<ListTeamTasksResponse>>;

public sealed record ListTeamTasksResponse(IReadOnlyList<TeamTaskItem> Items, int Total, int Page, int PageSize);

public sealed record TeamTaskItem(Guid Id, string Title, string Status, string Priority, Guid? ResponsibleId, DateTime? DueDate, DateTime UpdatedAt);

public sealed class ListTeamTasksHandler(ProjectsDbContext db) : IQueryHandler<ListTeamTasksQuery, Result<ListTeamTasksResponse>>
{
    public async Task<Result<ListTeamTasksResponse>> HandleAsync(ListTeamTasksQuery q, CancellationToken ct)
    {
        var query = db.WorkItems.AsNoTracking().Where(w => w.TenantId == q.TenantId);
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var items = await query.OrderByDescending(w => w.UpdatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new TeamTaskItem(
                w.Id.Value, w.Title,
                WorkItemStatus.FromId(w.StatusId).Name,
                WorkItemPriority.FromId(w.PriorityId).Name,
                w.ResponsibleId, w.DueDate, w.UpdatedAt))
            .ToListAsync(ct);
        return Result.Success(new ListTeamTasksResponse(items, total, page, pageSize));
    }
}
