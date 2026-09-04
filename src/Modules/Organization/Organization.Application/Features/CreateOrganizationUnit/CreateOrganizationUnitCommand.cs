using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Results;
using Microsoft.EntityFrameworkCore;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;

namespace Organization.Application.Features.CreateOrganizationUnit;

public sealed record CreateOrganizationUnitCommand(Guid TenantId, string Name, Guid? ParentId) : ICommand<Result<Guid>>;
public sealed class CreateOrganizationUnitValidator : Validator<CreateOrganizationUnitCommand>
{
    public CreateOrganizationUnitValidator()
    {
        RuleFor(x => x.TenantId != Guid.Empty, nameof(CreateOrganizationUnitCommand.TenantId), "TenantId required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Name) && x.Name.Trim().Length >= 2 && x.Name.Trim().Length <= 200, nameof(CreateOrganizationUnitCommand.Name), "Name 2..200");
    }
}

public sealed class CreateOrganizationUnitHandler(OrganizationDbContext db) : ICommandHandler<CreateOrganizationUnitCommand, Result<Guid>>
{
    public async Task<Result<Guid>> HandleAsync(CreateOrganizationUnitCommand cmd, CancellationToken ct)
    {
        var name = cmd.Name.Trim();
        OrganizationUnit? parent = null;
        HierarchyPath path;
        OrganizationUnitId? parentIdVo = null;
        if (cmd.ParentId.HasValue)
        {
            parent = await db.OrganizationUnits.FirstOrDefaultAsync(x => x.Id == new OrganizationUnitId(cmd.ParentId.Value) && x.TenantId == cmd.TenantId, ct);
            if (parent is null) return Result.Failure<Guid>(Error.NotFound("OrganizationUnit.ParentNotFound", "Parent not found"));
            parentIdVo = parent.Id;
            path = parent.HierarchyPath.Append(name);
        }
        else
        {
            path = HierarchyPath.Root(name);
        }
        var unit = OrganizationUnit.Create(cmd.TenantId, parentIdVo, name, path);
        db.OrganizationUnits.Add(unit);
        await db.SaveChangesAsync(ct);
        return Result.Success(unit.Id.Value);
    }
}
