using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Audit.Infrastructure.Persistence;

namespace Api.Features.Audit;

public sealed class ListAuditEntriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit/entries", async (HttpContext ctx, ISender sender, int? page, int? pageSize, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListAuditEntriesQuery(tenantId, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListAuditEntriesQuery(Guid TenantId, int Page, int PageSize) : IQuery<Result<PagedAuditResponse>>;

public sealed record PagedAuditResponse(IReadOnlyList<AuditEntryDto> Items, int Total, int Page, int PageSize);
public sealed record AuditEntryDto(Guid Id, string ActionType, string ResourceType, string ResourceId, DateTime Timestamp, string? Hash);

public sealed class ListAuditEntriesHandler(AuditDbContext db) : IQueryHandler<ListAuditEntriesQuery, Result<PagedAuditResponse>>
{
    public async Task<Result<PagedAuditResponse>> HandleAsync(ListAuditEntriesQuery q, CancellationToken ct)
    {
        var query = db.AuditEntries.AsNoTracking().Where(e => e.TenantId == q.TenantId);
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var entries = await query.OrderByDescending(e => e.Timestamp)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = entries.Select(e => new AuditEntryDto(
            e.Id.Value, e.Action.Name, e.ResourceType, e.ResourceId, e.Timestamp, e.Hash)).ToList();
        return Result.Success(new PagedAuditResponse(items, total, page, pageSize));
    }
}
