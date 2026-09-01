using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.EntityFrameworkCore;

using Organization.Domain.ValueObjects;
using Organization.Infrastructure.Persistence;

namespace Organization.Application.Features.MoveOrganizationUnit;

public sealed record MoveOrganizationUnitCommand(Guid TenantId, Guid UnitId, Guid? NewParentId) : IRequest<Result>;

public sealed class MoveOrganizationUnitHandler : IRequestHandler<MoveOrganizationUnitCommand, Result>
{
    private readonly OrganizationDbContext _db;
    public MoveOrganizationUnitHandler(OrganizationDbContext db) => _db = db;
    public async Task<Result> HandleAsync(MoveOrganizationUnitCommand cmd, CancellationToken ct)
    {
        var unit = await _db.OrganizationUnits.FirstOrDefaultAsync(x => x.Id.Value == cmd.UnitId && x.TenantId == cmd.TenantId, ct);
        if (unit == null) return Result.Failure(Error.NotFound("OrganizationUnit.NotFound", $"Unit {cmd.UnitId} not found"));
        var newPath = new HierarchyPath(new[] { "moved" });
        unit.Move(cmd.NewParentId != null ? new OrganizationUnitId(cmd.NewParentId.Value) : null, newPath);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}