using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.ProjectsMgmt.AddProjectMember;

public sealed record AddProjectMemberCommand(Guid ProjectId, Guid UserId, string Role, Guid TenantId) : ICommand<Result<Unit>>;

public sealed record Unit;

public sealed class AddProjectMemberValidator : Validator<AddProjectMemberCommand>
{
    public AddProjectMemberValidator()
    {
        RuleFor(x => x.ProjectId != Guid.Empty, nameof(AddProjectMemberCommand.ProjectId), "ProjectId required");
        RuleFor(x => x.UserId != Guid.Empty, nameof(AddProjectMemberCommand.UserId), "UserId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Role), nameof(AddProjectMemberCommand.Role), "Role required");
    }
}

public sealed class AddProjectMemberHandler(ProjectsDbContext db) : ICommandHandler<AddProjectMemberCommand, Result<Unit>>
{
    public async Task<Result<Unit>> HandleAsync(AddProjectMemberCommand cmd, CancellationToken ct)
    {
        var project = await db.Projects.Include(p => p.Members).FirstOrDefaultAsync(p => p.Id == new Projects.Domain.Ids.ProjectId(cmd.ProjectId) && p.TenantId == cmd.TenantId, ct);
        if (project is null) return Error.NotFound("Project.NotFound", "Project not found");
        var role = ProjectRole.FromName(cmd.Role);
        try
        {
            project.AddMember(cmd.UserId, role.Id);
        }
        catch (BuildingBlocks.Kernel.Domain.Rules.BusinessRuleValidationException ex)
        {
            return Error.Validation("Project.DuplicateMember", ex.Message);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(new Unit());
    }
}