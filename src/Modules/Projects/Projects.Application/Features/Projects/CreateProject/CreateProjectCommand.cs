using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Projects.Contracts.Dtos;
using Projects.Domain.Aggregates;
using Projects.Domain.Enumerations;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.ProjectsMgmt.CreateProject;

public sealed record CreateProjectCommand(string Name, Guid OwnerId, Guid ManagerId, string Status, string Priority, string Criticality, DateTime? DueDate, string? Description, Guid TenantId) : ICommand<Result<CreateProjectResponse>>;

public sealed class CreateProjectValidator : Validator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Name) && x.Name.Trim().Length >= 3 && x.Name.Trim().Length <= 200, nameof(CreateProjectCommand.Name), "Name must be 3..200");
        RuleFor(x => x.OwnerId != Guid.Empty, nameof(CreateProjectCommand.OwnerId), "OwnerId required");
        RuleFor(x => x.ManagerId != Guid.Empty, nameof(CreateProjectCommand.ManagerId), "ManagerId required");
    }
}

public sealed class CreateProjectHandler(ProjectsDbContext db) : ICommandHandler<CreateProjectCommand, Result<CreateProjectResponse>>
{
    public async Task<Result<CreateProjectResponse>> HandleAsync(CreateProjectCommand cmd, CancellationToken ct)
    {
        // Resolve enumerations by name
        var status = ProjectStatus.FromName(cmd.Status);
        var priority = ProjectPriority.FromName(cmd.Priority);
        var criticality = Criticality.FromName(cmd.Criticality);

        var project = Project.Create(cmd.TenantId, cmd.Name, cmd.OwnerId, cmd.ManagerId, status.Id, priority.Id, criticality.Id, null, cmd.DueDate, cmd.Description);
        // auto add owner as member? keep explicit but also ensure at least manager is member via add
        db.Projects.Add(project);
        // attempt to add owner as member; ignore duplicate
        try { project.AddMember(cmd.OwnerId, ProjectRole.Owner.Id); } catch { }
        if (cmd.ManagerId != cmd.OwnerId)
            try { project.AddMember(cmd.ManagerId, ProjectRole.Manager.Id); } catch { }

        await db.SaveChangesAsync(ct);

        var resp = new CreateProjectResponse(project.Id.Value, project.TenantId, project.Name, status.Name, priority.Name, criticality.Name, project.OwnerId, project.ManagerId, 1);
        return Result.Success(resp);
    }
}