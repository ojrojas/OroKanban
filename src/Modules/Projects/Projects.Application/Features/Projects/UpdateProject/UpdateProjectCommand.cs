using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Projects.Contracts.Dtos;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.ProjectsMgmt.UpdateProject;

public sealed record UpdateProjectCommand(Guid ProjectId, Guid TenantId, string Name, string Status, string Priority, string Criticality, DateTime? DueDate, string? Description) : ICommand<Result<ProjectDetailResponse>>;

public sealed class UpdateProjectValidator : Validator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.ProjectId != Guid.Empty, nameof(UpdateProjectCommand.ProjectId), "ProjectId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Name) && x.Name.Trim().Length >= 3 && x.Name.Trim().Length <= 200, nameof(UpdateProjectCommand.Name), "Name 3..200");
    }
}

public sealed class UpdateProjectHandler(ProjectsDbContext db) : ICommandHandler<UpdateProjectCommand, Result<ProjectDetailResponse>>
{
    public async Task<Result<ProjectDetailResponse>> HandleAsync(UpdateProjectCommand cmd, CancellationToken ct)
    {
        var pid = new Projects.Domain.Ids.ProjectId(cmd.ProjectId);
        var p = await db.Projects.FirstOrDefaultAsync(x=> x.Id == pid && x.TenantId==cmd.TenantId, ct);
        if(p is null) return Error.NotFound("Project.NotFound","Project not found");
        ProjectStatus status; ProjectPriority priority; Criticality criticality;
        try{ status = ProjectStatus.FromName(cmd.Status);} catch{ return Error.Validation("ProjectStatus.Invalid",$"Status '{cmd.Status}' invalid");}
        try{ priority = ProjectPriority.FromName(cmd.Priority);} catch{ priority = ProjectPriority.Medium; }
        try{ criticality = Criticality.FromName(cmd.Criticality);} catch{ criticality = Criticality.Medium; }
        p.UpdateDetails(cmd.Name, cmd.Description, status.Id, priority.Id, criticality.Id, cmd.DueDate);
        await db.SaveChangesAsync(ct);
        var members = p.Members.Select(m=> new ProjectMemberDto(m.UserId, ProjectRole.FromId(m.RoleId).Name, m.JoinedAt)).ToList();
        var milestones = p.Milestones.Select(m=> new MilestoneDto(m.Id, m.Title, m.DueDate, m.IsReached)).ToList();
        return Result.Success(new ProjectDetailResponse(p.Id.Value, p.TenantId, p.Name, p.Description, status.Name, priority.Name, criticality.Name, p.OwnerId, p.ManagerId, p.DueDate, p.UpdatedAt, members, milestones));
    }
}
