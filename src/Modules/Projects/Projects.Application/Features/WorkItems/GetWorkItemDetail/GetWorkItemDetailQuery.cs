using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Contracts.Dtos;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.GetWorkItemDetail;

public sealed record GetWorkItemDetailQuery(Guid WorkItemId, Guid TenantId) : IQuery<Result<WorkItemDetailResponse>>;

public sealed class GetWorkItemDetailHandler(ProjectsDbContext db) : IQueryHandler<GetWorkItemDetailQuery, Result<WorkItemDetailResponse>>
{
    public async Task<Result<WorkItemDetailResponse>> HandleAsync(GetWorkItemDetailQuery q, CancellationToken ct)
    {
        var w = await db.WorkItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == new Projects.Domain.Ids.WorkItemId(q.WorkItemId) && x.TenantId == q.TenantId, ct);
        if (w is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        var dto = new WorkItemDetailResponse(w.Id.Value, w.ProjectId, w.ParentId, w.Title, w.Description,
            Projects.Domain.Enumerations.WorkItemType.FromId(w.TypeId).Name,
            Projects.Domain.Enumerations.WorkItemStatus.FromId(w.StatusId).Name,
            Projects.Domain.Enumerations.WorkItemPriority.FromId(w.PriorityId).Name,
            Projects.Domain.Enumerations.Criticality.FromId(w.CriticalityId).Name,
            w.OwnerId, w.ResponsibleId, w.ReviewerId, w.DueDate, w.ProgressPercent, w.Tags, w.Deliverables, w.Observations, w.Version, w.UpdatedAt, w.TenantId, w.IsOverdue(DateTime.UtcNow), [] , w.EstimatedHours, w.ActualHours, w.StartedAt, w.ReopenedCount, false);
        return Result.Success(dto);
    }
}