using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.HistoryQueries;

public sealed record GetWorkItemHistoryQuery(Guid WorkItemId, Guid TenantId) : IQuery<Result<IReadOnlyList<WorkItemHistoryDto>>>;

public sealed class GetWorkItemHistoryHandler(ProjectsDbContext db) : IQueryHandler<GetWorkItemHistoryQuery, Result<IReadOnlyList<WorkItemHistoryDto>>>
{
    public async Task<Result<IReadOnlyList<WorkItemHistoryDto>>> HandleAsync(GetWorkItemHistoryQuery q, CancellationToken ct)
    {
        var list = await db.WorkItemHistories.AsNoTracking().Where(h=> h.WorkItemId==q.WorkItemId && h.TenantId==q.TenantId).OrderByDescending(h=> h.CreatedAt).ToListAsync(ct);
        var dto = list.Select(h=> new WorkItemHistoryDto(h.Id, h.WorkItemId, h.Field, h.FromJson, h.ToJson, h.Comment, h.CreatedAt, h.ActorId)).ToList();
        return Result.Success<IReadOnlyList<WorkItemHistoryDto>>(dto);
    }
}
