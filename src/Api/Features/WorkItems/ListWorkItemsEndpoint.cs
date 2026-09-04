using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace Api.Features.WorkItems;

public sealed class ListWorkItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/work-items", async (HttpContext ctx, ISender sender, int? page, int? pageSize, string? q, string? filter, string? sort, string? sortDir, string? assignee, Guid? projectId, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var callerId = ctx.User.FindFirst("sub")?.Value is string sub && Guid.TryParse(sub, out var sg) ? sg : Guid.Empty;
            var effectiveAssignee = assignee == "me" ? callerId : (Guid?)null;
            var result = await sender.SendAsync(new ListWorkItemsQuery(tenantId, projectId, page ?? 1, pageSize ?? 20, q, filter, effectiveAssignee, sort, sortDir), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListWorkItemsQuery(Guid TenantId, Guid? ProjectId, int Page, int PageSize, string? Search, string? StatusFilter, Guid? AssigneeFilter, string? SortBy, string? SortDir) : IQuery<Result<PagedWorkItemsResponse>>;

public sealed record PagedWorkItemsResponse(IReadOnlyList<WorkItemListItem> Items, int Total, int Page, int PageSize);
public sealed record WorkItemListItem(Guid Id, Guid ProjectId, Guid? ParentId, string Title, string Type, string Status, string Priority, string Criticality, Guid? ResponsibleId, DateTime? DueDate, int ProgressPercent, DateTime UpdatedAt);

public sealed class ListWorkItemsHandler(ProjectsDbContext db) : IQueryHandler<ListWorkItemsQuery, Result<PagedWorkItemsResponse>>
{
    public async Task<Result<PagedWorkItemsResponse>> HandleAsync(ListWorkItemsQuery q, CancellationToken ct)
    {
        var query = db.WorkItems.AsNoTracking().Where(w => w.TenantId == q.TenantId);
        if (q.ProjectId.HasValue) query = query.Where(w => w.ProjectId == q.ProjectId.Value);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(w => w.Title.Contains(q.Search));
        if (q.AssigneeFilter.HasValue) query = query.Where(w => w.ResponsibleId == q.AssigneeFilter.Value);
        if (!string.IsNullOrWhiteSpace(q.StatusFilter))
        {
            var statuses = q.StatusFilter.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = WorkItemStatus.GetAll().Where(s => statuses.Contains(s.Name)).Select(s => s.Id).ToHashSet();
            query = query.Where(w => ids.Contains(w.StatusId));
        }
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        query = q.SortBy?.ToLowerInvariant() switch
        {
            "priority" => q.SortDir == "desc" ? query.OrderByDescending(w => w.PriorityId) : query.OrderBy(w => w.PriorityId),
            "duedate" => q.SortDir == "desc" ? query.OrderByDescending(w => w.DueDate) : query.OrderBy(w => w.DueDate),
            _ => query.OrderByDescending(w => w.UpdatedAt)
        };
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(w => new WorkItemListItem(w.Id.Value, w.ProjectId, w.ParentId, w.Title,
                WorkItemType.FromId(w.TypeId).Name,
                WorkItemStatus.FromId(w.StatusId).Name,
                WorkItemPriority.FromId(w.PriorityId).Name,
                Criticality.FromId(w.CriticalityId).Name,
                w.ResponsibleId, w.DueDate, w.ProgressPercent, w.UpdatedAt))
            .ToListAsync(ct);
        return Result.Success(new PagedWorkItemsResponse(items, total, page, pageSize));
    }
}
