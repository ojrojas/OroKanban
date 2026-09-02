using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Projects.Infrastructure.Persistence;

namespace Api.Features.Planning;

public sealed class ListMilestonesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/planning/milestones", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListMilestonesQuery(tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListMilestonesQuery(Guid TenantId) : IQuery<Result<ListMilestonesResponse>>;

public sealed record ListMilestonesResponse(IReadOnlyList<MilestoneListItem> Items);

public sealed record MilestoneListItem(Guid Id, Guid ProjectId, string Title, DateTime? DueDate, bool IsReached, DateTime? ReachedAt);

public sealed class ListMilestonesHandler(ProjectsDbContext db) : IQueryHandler<ListMilestonesQuery, Result<ListMilestonesResponse>>
{
    public async Task<Result<ListMilestonesResponse>> HandleAsync(ListMilestonesQuery q, CancellationToken ct)
    {
        var projects = await db.Projects.AsNoTracking()
            .Include(p => p.Milestones)
            .Where(p => p.TenantId == q.TenantId)
            .ToListAsync(ct);
        var items = projects.SelectMany(p => p.Milestones.Select(m => new MilestoneListItem(
            m.Id, p.Id.Value, m.Title, m.DueDate, m.IsReached, m.ReachedAt)))
            .OrderBy(m => m.DueDate)
            .ToList();
        return Result.Success(new ListMilestonesResponse(items));
    }
}
