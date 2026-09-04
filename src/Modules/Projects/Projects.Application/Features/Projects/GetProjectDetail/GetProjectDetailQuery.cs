using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.ProjectsMgmt.GetProjectDetail;

public sealed record GetProjectDetailQuery(Guid ProjectId, Guid TenantId) : IQuery<Result<ProjectDetailResponse>>;

public sealed class GetProjectDetailHandler(ProjectsDbContext db) : IQueryHandler<GetProjectDetailQuery, Result<ProjectDetailResponse>>
{
    public async Task<Result<ProjectDetailResponse>> HandleAsync(GetProjectDetailQuery q, CancellationToken ct)
    {
        var pid = new Projects.Domain.Ids.ProjectId(q.ProjectId);
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x=> x.Id == pid && x.TenantId==q.TenantId, ct);
        if(p is null) return Error.NotFound("Project.NotFound","Project not found");
        var projMembers = await db.Projects.AsNoTracking().Where(x=> x.Id == pid).SelectMany(x=> x.Members).ToListAsync(ct);
        // p already has members via Owned; but AsNoTracking with owns many requires Include
        var full = await db.Projects.Include(x=> x.Members).Include(x=> x.Milestones).FirstOrDefaultAsync(x=> x.Id == pid, ct);
        if(full is null) return Error.NotFound("Project.NotFound","Project not found");
        var status = ProjectStatus.FromId(full.StatusId).Name;
        var priority = ProjectPriority.FromId(full.PriorityId).Name;
        var criticality = Criticality.FromId(full.CriticalityId).Name;
        var members = full.Members.Select(m=> new ProjectMemberDto(m.UserId, ProjectRole.FromId(m.RoleId).Name, m.JoinedAt)).ToList();
        var milestones = full.Milestones.Select(m=> new MilestoneDto(m.Id, m.Title, m.DueDate, m.IsReached)).ToList();
        return Result.Success(new ProjectDetailResponse(full.Id.Value, full.TenantId, full.Name, full.Description, status, priority, criticality, full.OwnerId, full.ManagerId, full.DueDate, full.UpdatedAt, members, milestones));
    }
}
