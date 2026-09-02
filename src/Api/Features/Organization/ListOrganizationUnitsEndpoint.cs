using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Organization.Infrastructure.Persistence;

namespace Api.Features.Organization;

public sealed class ListOrganizationUnitsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organization/units", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListOrgUnitsQuery(tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed class AdminListOrganizationUnitsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/organization-units", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListOrgUnitsQuery(tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListOrgUnitsQuery(Guid TenantId) : IQuery<Result<OrgUnitsResponse>>;

public sealed record OrgUnitsResponse(IReadOnlyList<OrgUnitDto> Items);
public sealed record OrgUnitDto(Guid Id, string Name, Guid? ParentId, string HierarchyPath);

public sealed class ListOrgUnitsHandler(OrganizationDbContext db) : IQueryHandler<ListOrgUnitsQuery, Result<OrgUnitsResponse>>
{
    public async Task<Result<OrgUnitsResponse>> HandleAsync(ListOrgUnitsQuery q, CancellationToken ct)
    {
        var units = await db.OrganizationUnits.AsNoTracking()
            .Where(u => u.TenantId == q.TenantId).OrderBy(u => u.Name).ToListAsync(ct);
        var items = units.Select(u => new OrgUnitDto(u.Id.Value, u.Name, u.ParentId?.Value, u.HierarchyPath.ToPathString())).ToList();
        return Result.Success(new OrgUnitsResponse(items));
    }
}
