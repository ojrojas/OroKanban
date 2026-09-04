using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.Queries;

public sealed record GetKanbanBoardQuery(Guid ProjectId, Guid TenantId, string? StatusFilter, Guid? AssigneeFilter, Guid? EpicFilter, string? PriorityFilter, string? CriticalityFilter, string? TagsFilter, string? Swimlane, string? SortBy, string? SortDir, int Page = 1, int PageSize = 20) : IQuery<Result<KanbanBoardResponse>>;

public sealed class GetKanbanBoardHandler(ProjectsDbContext db) : IQueryHandler<GetKanbanBoardQuery, Result<KanbanBoardResponse>>
{
    public async Task<Result<KanbanBoardResponse>> HandleAsync(GetKanbanBoardQuery q, CancellationToken ct)
    {
        if (q.ProjectId == Guid.Empty) return Error.Validation("Board.ProjectRequired", "projectId is required");

        var query = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == q.ProjectId && w.TenantId == q.TenantId);

        // status filter csv
        if (!string.IsNullOrWhiteSpace(q.StatusFilter))
        {
            var statuses = q.StatusFilter.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = WorkItemStatus.GetAll().Where(s => statuses.Contains(s.Name)).Select(s => s.Id).ToHashSet();
            query = query.Where(w => ids.Contains(w.StatusId));
        }
        if (q.AssigneeFilter.HasValue)
            query = query.Where(w => w.ResponsibleId == q.AssigneeFilter.Value);
        // tags filter csv and
        if (!string.IsNullOrWhiteSpace(q.TagsFilter))
        {
            var tags = q.TagsFilter.Split(',').Select(t => t.Trim().ToLowerInvariant()).ToList();
            foreach (var t in tags) query = query.Where(w => w.TagsJson != null && w.TagsJson.Contains(t));
        }

        // total count before pagination
        var total = await query.CountAsync(ct);

        // sorting
        query = (q.SortBy?.ToLowerInvariant()) switch
        {
            "priority" => q.SortDir == "desc" ? query.OrderByDescending(w => w.PriorityId) : query.OrderBy(w => w.PriorityId),
            "criticality" => q.SortDir == "desc" ? query.OrderByDescending(w => w.CriticalityId) : query.OrderBy(w => w.CriticalityId),
            "duedate" => q.SortDir == "desc" ? query.OrderByDescending(w => w.DueDate) : query.OrderBy(w => w.DueDate),
            _ => query.OrderByDescending(w => w.UpdatedAt)
        };

        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        // group into columns by status (all 6 statuses ordered)
        var statusesOrdered = WorkItemStatus.GetAll().OrderBy(s => s.Id).ToList();
        var now = DateTime.UtcNow;
        var overdueCount = await db.WorkItems.CountAsync(w => w.ProjectId == q.ProjectId && w.TenantId == q.TenantId && w.DueDate.HasValue && w.DueDate < now && w.StatusId != WorkItemStatus.Completed.Id, ct);

        var allForColumns = await db.WorkItems.AsNoTracking().Where(w => w.ProjectId == q.ProjectId && w.TenantId == q.TenantId).ToListAsync(ct);
        // For filtered view, columns are built from filtered items (paginated) but counts reflect filtered total per status?
        // Simpler: counts per status from filtered query before pagination
        var filteredAll = await query.ToListAsync(ct); // already filtered but we paginated; redo without pagination for counts
        // We already have total but need per status; query again without skip/take
        var countsQuery = db.WorkItems.AsNoTracking().Where(w => w.ProjectId == q.ProjectId && w.TenantId == q.TenantId);
        if (!string.IsNullOrWhiteSpace(q.StatusFilter))
        {
            var statuses = q.StatusFilter.Split(',').Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var ids = WorkItemStatus.GetAll().Where(s => statuses.Contains(s.Name)).Select(s => s.Id).ToHashSet();
            countsQuery = countsQuery.Where(w => ids.Contains(w.StatusId));
        }
        if (q.AssigneeFilter.HasValue) countsQuery = countsQuery.Where(w => w.ResponsibleId == q.AssigneeFilter.Value);

        var columns = new List<BoardColumnDto>();
        foreach (var s in statusesOrdered)
        {
            var colItems = items.Where(w => w.StatusId == s.Id).Select(w => new BoardItemDto(w.Id.Value, w.Title, WorkItemType.FromId(w.TypeId).Name, s.Name, WorkItemPriority.FromId(w.PriorityId).Name, Criticality.FromId(w.CriticalityId).Name, w.ResponsibleId, w.DueDate, w.IsOverdue(now), w.ProgressPercent, w.Tags, w.ParentId, null, false, w.Version, w.UpdatedAt, w.EstimatedHours, w.ActualHours)).ToList();
            var count = await countsQuery.CountAsync(w => w.StatusId == s.Id, ct);
            columns.Add(new BoardColumnDto(s.Name, s.Id, count, colItems));
        }

        var resp = new KanbanBoardResponse(q.ProjectId, DateTime.UtcNow, columns, [], page, pageSize, total, overdueCount);
        return Result.Success(resp);
    }
}

public sealed record GetMyTasksQuery(Guid UserId, Guid TenantId, int Page = 1, int PageSize = 20) : IQuery<Result<IReadOnlyList<WorkItemDetailResponse>>>;

public sealed record GetTeamTasksQuery(Guid ManagerId, Guid TenantId, int Page = 1, int PageSize = 20) : IQuery<Result<IReadOnlyList<WorkItemDetailResponse>>>;