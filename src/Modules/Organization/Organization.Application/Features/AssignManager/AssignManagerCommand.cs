using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using Organization.Contracts;
using Organization.Domain.Aggregates;
using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Organization.Application.Features.AssignManager;

public sealed record AssignManagerCommand(
    Guid TenantId,
    Guid ManagerId,
    Guid SubordinateId,
    string Type,
    Guid? OrganizationUnitId
) : IRequest<Result<Guid>>;

public sealed class AssignManagerValidator : BuildingBlocks.CQRS.Validation.Validator<AssignManagerCommand>
{
    public AssignManagerValidator()
    {
        RuleFor(x => x.ManagerId != Guid.Empty, nameof(AssignManagerCommand.ManagerId), "ManagerId is required");
        RuleFor(x => x.SubordinateId != Guid.Empty, nameof(AssignManagerCommand.SubordinateId), "SubordinateId is required");
        RuleFor(x => !string.IsNullOrWhiteSpace(x.Type), nameof(AssignManagerCommand.Type), "Type is required");
    }
}

public sealed class AssignManagerHandler : IRequestHandler<AssignManagerCommand, Result<Guid>>
{
    private readonly OrganizationDbContext _db;
    private readonly IManagementHierarchy _hierarchy;

    public AssignManagerHandler(OrganizationDbContext db, IManagementHierarchy hierarchy)
    {
        _db = db;
        _hierarchy = hierarchy;
    }

    public async Task<Result<Guid>> HandleAsync(AssignManagerCommand cmd, CancellationToken ct)
    {
        var ancestors = await _hierarchy.GetAncestorsAsync(cmd.TenantId, cmd.SubordinateId, ct);
        var relationship = ManagementRelationship.Create(
            cmd.TenantId,
            cmd.ManagerId,
            cmd.SubordinateId,
            cmd.Type,
            cmd.OrganizationUnitId != null ? new OrganizationUnitId(cmd.OrganizationUnitId.Value) : null,
            DateTime.UtcNow,
            null,
            ancestors);

        _db.ManagementRelationships.Add(relationship);
        await _db.SaveChangesAsync(ct);
        return relationship.Id.Value;
    }
}
