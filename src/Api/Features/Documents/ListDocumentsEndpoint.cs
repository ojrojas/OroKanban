using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using Documents.Infrastructure.Persistence;

namespace Api.Features.Documents;

public sealed class ListDocumentsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/documents", async (HttpContext ctx, ISender sender, int? page, int? pageSize, string? filter, string? q, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListDocumentsQuery(tenantId, page ?? 1, pageSize ?? 20, filter, q), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListDocumentsQuery(Guid TenantId, int Page, int PageSize, string? Filter, string? Search) : IQuery<Result<PagedDocumentsResponse>>;

public sealed record PagedDocumentsResponse(IReadOnlyList<DocumentListItem> Items, int Total, int Page, int PageSize);
public sealed record DocumentListItem(Guid Id, string Name, string MimeType, long Size, Guid CreatedBy, DateTime CreatedAt, string Status);

public sealed class ListDocumentsHandler(DocumentsDbContext db) : IQueryHandler<ListDocumentsQuery, Result<PagedDocumentsResponse>>
{
    public async Task<Result<PagedDocumentsResponse>> HandleAsync(ListDocumentsQuery q, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.TenantId == q.TenantId);
        if (!string.IsNullOrWhiteSpace(q.Search)) query = query.Where(d => d.Name.Contains(q.Search));
        if (!string.IsNullOrWhiteSpace(q.Filter)) query = query.Where(d => d.Status.Name == q.Filter);
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 100);
        var items = await query.OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DocumentListItem(d.Id.Value, d.Name, d.MimeType, d.Size, d.CreatedBy, d.CreatedAt, d.Status.Name))
            .ToListAsync(ct);
        return Result.Success(new PagedDocumentsResponse(items, total, page, pageSize));
    }
}
