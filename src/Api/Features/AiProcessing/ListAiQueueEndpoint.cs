using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.EntityFrameworkCore;

using AiProcessing.Infrastructure.Persistence;

namespace Api.Features.AiProcessing;

public sealed class ListAiQueueEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai-queue", async (HttpContext ctx, ISender sender, CancellationToken ct) =>
        {
            var tenantId = ctx.User.FindFirst("tenant_id")?.Value is string tid && Guid.TryParse(tid, out var tg) ? tg : Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new ListAiQueueQuery(tenantId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}

public sealed record ListAiQueueQuery(Guid TenantId) : IQuery<Result<AiQueueResponse>>;

public sealed record AiQueueResponse(IReadOnlyList<AiQueueItem> Items);
public sealed record AiQueueItem(Guid Id, int OperationTypeId, int OperationStatusId, string ModelName, DateTime CreatedAt, DateTime? CompletedAt);

public sealed class ListAiQueueHandler(AiProcessingDbContext db) : IQueryHandler<ListAiQueueQuery, Result<AiQueueResponse>>
{
    public async Task<Result<AiQueueResponse>> HandleAsync(ListAiQueueQuery q, CancellationToken ct)
    {
        var items = await db.LlmOperations.AsNoTracking()
            .Where(o => o.TenantId == q.TenantId)
            .OrderByDescending(o => o.CreatedAt).Take(50)
            .Select(o => new AiQueueItem(o.Id.Value, o.OperationTypeId, o.OperationStatusId, o.ModelName, o.CreatedAt, o.CompletedAt))
            .ToListAsync(ct);
        return Result.Success(new AiQueueResponse(items));
    }
}
