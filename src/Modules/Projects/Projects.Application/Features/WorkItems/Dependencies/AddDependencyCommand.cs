using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Projects.Domain.Aggregates;
using Projects.Domain.Enumerations;
using Projects.Domain.Services;
using Projects.Infrastructure.Persistence;

namespace ProjectsApp.Features.WorkItems.Dependencies;

public sealed record AddDependencyCommand(Guid DependentId, Guid PrincipalId, Guid TenantId, string Type) : ICommand<Result<Guid>>;

public sealed class AddDependencyValidator : Validator<AddDependencyCommand>
{
    public AddDependencyValidator()
    {
        RuleFor(x => x.DependentId != Guid.Empty, nameof(AddDependencyCommand.DependentId), "DependentId required");
        RuleFor(x => x.PrincipalId != Guid.Empty, nameof(AddDependencyCommand.PrincipalId), "PrincipalId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Type), nameof(AddDependencyCommand.Type), "Type required");
        RuleFor(x => x.DependentId != x.PrincipalId, nameof(AddDependencyCommand.PrincipalId), "Dependent and principal must differ");
    }
}

public sealed class AddDependencyHandler(ProjectsDbContext db, IDependencyCycleDetector detector) : ICommandHandler<AddDependencyCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(AddDependencyCommand cmd, CancellationToken ct)
    {
        var depType = DependencyType.FromName(cmd.Type);

        var dependent = await db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(cmd.DependentId) && w.TenantId == cmd.TenantId, ct);
        var principal = await db.WorkItems.AsNoTracking().FirstOrDefaultAsync(w => w.Id == new Projects.Domain.Ids.WorkItemId(cmd.PrincipalId) && w.TenantId == cmd.TenantId, ct);
        if (dependent is null || principal is null) return Error.NotFound("WorkItem.NotFound", "Work item not found");
        if (dependent.ProjectId != principal.ProjectId && depType.Id != DependencyType.RelatedTo.Id)
            return Error.Validation("Dependency.CrossProject", "Cross-project only allowed for RelatedTo");

        var exists = await db.WorkItemDependencies.AnyAsync(d => d.DependentId == cmd.DependentId && d.PrincipalId == cmd.PrincipalId, ct);
        if (exists) return Error.Validation("Dependency.Duplicate", "Dependency already exists");

        // load existing non-RelatedTo edges for project
        var existingEdges = await db.WorkItemDependencies.AsNoTracking()
            .Where(d => d.TenantId == cmd.TenantId)
            .Where(d => db.WorkItems.Any(w => w.Id == new Projects.Domain.Ids.WorkItemId(d.DependentId) && w.ProjectId == dependent.ProjectId))
            .Select(d => new { d.DependentId, d.PrincipalId, d.TypeId })
            .ToListAsync(ct);

        var existing = existingEdges.Select(e => (e.DependentId, e.PrincipalId, e.TypeId)).ToList();
        var candidate = (cmd.DependentId, cmd.PrincipalId, depType.Id);
        if (detector.HasCycle(existing, candidate))
            return Error.Validation("Dependency.Circular", "Circular dependency");

        var dep = WorkItemDependency.Create(cmd.TenantId, cmd.DependentId, cmd.PrincipalId, depType.Id);
        db.WorkItemDependencies.Add(dep);
        await db.SaveChangesAsync(ct);
        return Result.Success(dep.Id.Value);
    }
}

public sealed record RemoveDependencyCommand(Guid DependencyId, Guid TenantId) : ICommand<Result>;

public sealed class RemoveDependencyHandler(ProjectsDbContext db) : ICommandHandler<RemoveDependencyCommand, Result>
{
    public async Task<Result> HandleAsync(RemoveDependencyCommand cmd, CancellationToken ct)
    {
        var dep = await db.WorkItemDependencies.FirstOrDefaultAsync(d => d.Id == new Projects.Domain.Ids.WorkItemDependencyId(cmd.DependencyId) && d.TenantId == cmd.TenantId, ct);
        if (dep is null) return Error.NotFound("Dependency.NotFound", "Dependency not found");
        db.WorkItemDependencies.Remove(dep);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}